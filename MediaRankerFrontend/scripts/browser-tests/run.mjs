import { spawn, execFileSync } from "node:child_process";
import { randomUUID } from "node:crypto";
import { createWriteStream } from "node:fs";
import { createServer, connect } from "node:net";
import { mkdir, open, readFile, readdir, stat, writeFile } from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const frontend = path.resolve(here, "../..");
const repo = path.resolve(frontend, "..");
const backend = path.join(repo, "MediaRankerServer");
const projectFile = path.join(backend, "MediaRankerServer.csproj");
const composeFile = path.join(here, "compose.e2e.yml");
const mode = process.argv[2];

if (!["e2e", "browser"].includes(mode)) {
  console.error("Usage: node scripts/browser-tests/run.mjs <e2e|browser>");
  process.exit(2);
}

const runId = `${new Date()
  .toISOString()
  .replaceAll(/[-:.TZ]/g, "")
  .slice(0, 14)}-${randomUUID().slice(0, 8)}`;
const runDir = path.join(here, "runs", runId);
const processes = new Set();
const portOwners = [];
const reportFile = path.join(runDir, "runner.log");
let dockerProject;
let ownsDocker = false;
let cleaning = false;
let signalCode;

await mkdir(runDir, { recursive: true });
const logHandle = await open(reportFile, "a");
const log = async (message) => {
  const line = `[${new Date().toISOString()}] ${message}\n`;
  await logHandle.write(line);
  process.stdout.write(line);
};
const fail = (message) => {
  throw new Error(message);
};

function runFile(name) {
  return path.join(runDir, name);
}

async function spawnOwned(label, executable, args, options = {}) {
  await log(`Starting owned child ${label} (${path.basename(executable)}).`);
  const out = createWriteStream(runFile(`${label}.log`), { flags: "a" });
  await new Promise((resolve, reject) => {
    out.once("open", resolve);
    out.once("error", reject);
  });
  const outputClosed = new Promise((resolve) => out.once("close", resolve));
  try {
    assertRunning();
  } catch (error) {
    out.end();
    await outputClosed;
    throw error;
  }
  const child = spawn(executable, args, {
    cwd: options.cwd ?? frontend,
    env: options.env ?? process.env,
    stdio: ["ignore", out, out],
    windowsHide: true,
    detached: process.platform !== "win32",
  });
  child.once("error", (error) => {
    child.spawnError = error;
    out.end();
  });
  child.outputClosed = outputClosed;
  out.on("error", (error) => {
    child.spawnError ??= error;
    if (child.exitCode === null) child.kill();
    out.end();
  });
  processes.add(child);
  child.once("exit", () => out.end());
  await log(`Owned child ${label} started with PID ${child.pid ?? "unavailable"}.`);
  return child;
}

async function waitExit(child) {
  if (child.spawnError) throw child.spawnError;
  if (child.exitCode === null && child.signalCode === null) {
    await new Promise((resolve, reject) => {
      child.once("error", reject);
      child.once("exit", resolve);
    });
  }
  await child.outputClosed;
  return child.exitCode ?? 1;
}

async function waitExitWithDeadline(child, timeoutMs, label) {
  if (child.spawnError) throw child.spawnError;
  let timer;
  let timedOut = false;
  try {
    await Promise.race([
      waitExit(child),
      new Promise((_, reject) => {
        timer = setTimeout(
          () => {
            timedOut = true;
            reject(new Error(`${label} exceeded its ${timeoutMs / 1000}s deadline.`));
          },
          timeoutMs,
        );
      }),
    ]);
  } catch (error) {
    if (timedOut) {
      const stopped = await killOwned(child);
      fail(`${error.message} ${stopped ? "The owned process tree was stopped." : "The runner could not confirm that the owned process tree stopped."} See ${runFile(`${label}.log`)}.`);
    }
    throw error;
  } finally {
    clearTimeout(timer);
  }
  return waitExit(child);
}

function delay(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function reservePort(host = "127.0.0.1") {
  const server = createServer();
  await new Promise((resolve, reject) => {
    server.once("error", reject);
    server.listen(0, host, resolve);
  });
  const port = server.address().port;
  await new Promise((resolve) => server.close(resolve));
  return port;
}

async function portOpen(host, port) {
  return new Promise((resolve) => {
    const socket = connect({ host, port });
    socket.setTimeout(700);
    socket.once("connect", () => {
      socket.destroy();
      resolve(true);
    });
    socket.once("error", () => resolve(false));
    socket.once("timeout", () => {
      socket.destroy();
      resolve(false);
    });
  });
}

async function assertPortAvailable(label, host, port) {
  if (await portOpen(host, port))
    fail(`${label} port ${host}:${port} became occupied before startup. Refusing to attach to the existing listener.`);
}

async function readOwnedLog(label) {
  return readFile(runFile(`${label}.log`), "utf8").catch(() => "");
}

async function waitFor(label, probe, child, timeoutMs = 90_000) {
  const deadline = Date.now() + timeoutMs;
  let lastError;
  while (Date.now() < deadline) {
    assertRunning();
    if (child?.spawnError)
      fail(`${label} could not start: ${child.spawnError.message}.`);
    if (lastError) fail(`${label} could not start: ${lastError.message}.`);
    if (child?.exitCode !== null && child?.exitCode !== undefined) {
      fail(
        `${label} exited with code ${child.exitCode}; see ${path.join(runDir, `${label}.log`)}.`,
      );
    }
    try {
      const value = await probe();
      if (value) return value;
    } catch (error) {
      lastError = error;
    }
    await delay(350);
  }
  fail(
    `${label} did not become ready within ${timeoutMs / 1000}s${lastError ? ` (${lastError.message})` : ""}. See ${path.join(runDir, `${label}.log`)}.`,
  );
}

function execCapture(executable, args, options = {}) {
  try {
    return execFileSync(executable, args, {
      cwd: options.cwd ?? frontend,
      env: options.env ?? process.env,
      encoding: "utf8",
      stdio: ["ignore", "pipe", "pipe"],
      windowsHide: true,
      timeout: options.timeout ?? 30_000,
      maxBuffer: options.maxBuffer ?? 10 * 1024 * 1024,
    });
  } catch (error) {
    const detail = `${error.stdout ?? ""}${error.stderr ?? ""}`.trim();
    throw new Error(
      `${options.label ?? executable} failed${detail ? `: ${detail}` : "."}`,
    );
  }
}

function commandExists(executable, args, label) {
  execCapture(executable, args, { label, timeout: 60_000 });
}

function assertRunning() {
  if (signalCode !== undefined)
    fail("Startup stopped after an interrupt request.");
}

async function runOwnedCommand(label, executable, args, options = {}) {
  assertRunning();
  const child = await spawnOwned(label, executable, args, options);
  let code;
  try {
    code = await waitExitWithDeadline(
      child,
      options.timeoutMs ?? 5 * 60_000,
      label,
    );
  } finally {
    if (child.exitCode !== null || child.signalCode !== null)
      processes.delete(child);
  }
  assertRunning();
  if (code !== 0)
    fail(`${label} exited with code ${code}. See ${runFile(`${label}.log`)}.`);
  return readFile(runFile(`${label}.log`), "utf8");
}

function ensureNoDeveloperDotnetProcess() {
  const backendNeedle = backend.toLowerCase();
  if (process.platform === "win32") {
    const binNeedle = path
      .join(backend, "bin")
      .toLowerCase()
      .replaceAll("'", "''");
    const ps = String.raw`$root = '${backendNeedle.replaceAll("'", "''")}'; $bin = '${binNeedle}'; Get-CimInstance Win32_Process | Where-Object { (($_.Name -match '^(dotnet|MediaRankerServer)(\.exe)?$') -and $_.CommandLine -and $_.CommandLine.ToLowerInvariant().Contains($root) -and ($_.CommandLine -match '(?i)dotnet(?:\.exe)?["'']?\s+(run|watch)\b|MediaRankerServer(\.dll|\.exe)?')) -or (($_.Name -ieq 'MediaRankerServer.exe') -and $_.ExecutablePath -and $_.ExecutablePath.ToLowerInvariant().StartsWith($bin)) -or (($_.Name -ieq 'dotnet.exe') -and $_.CommandLine -match '(?i)\bdotnet(?:\.exe)?["'']?\s+(run|watch)\b') -or ($_.Name -ieq 'iisexpress.exe') } | Select-Object ProcessId,Name,@{Name='Reason';Expression={if ($_.Name -ieq 'iisexpress.exe') {'IIS Express may host this checkout'} elseif ($_.CommandLine -match '(?i)\bdotnet(?:\.exe)?["'']?\s+(run|watch)\b' -and -not $_.CommandLine.ToLowerInvariant().Contains($root)) {'dotnet run/watch may own shared output'} else {'MediaRankerServer from this checkout'}}} | ConvertTo-Json -Compress`;
    const result = execCapture(
      "powershell.exe",
      ["-NoProfile", "-NonInteractive", "-Command", ps],
      { label: "Developer API process check" },
    ).trim();
    if (result && result !== "null") {
      fail(
        `A .NET process from this checkout may own shared bin/obj output. Stop it before E2E; no process was stopped. Matching process IDs/names: ${result}`,
      );
    }
    return;
  }

  const listing = execCapture("ps", ["-eo", "pid=,comm=,args="], {
    label: "Developer API process check",
  });
  const inspectProcessPath = (pid, kind) => {
    try {
      if (process.platform === "linux")
        return execCapture("readlink", ["-f", `/proc/${pid}/${kind}`], {
          label: "Developer API process ownership check",
        }).trim().toLowerCase();
      if (process.platform === "darwin" && kind === "cwd") {
        const result = execCapture("lsof", ["-a", "-p", String(pid), "-d", "cwd", "-Fn"], {
          label: "Developer API process ownership check",
        });
        return result.split(/\r?\n/).find((line) => line.startsWith("n"))?.slice(1).toLowerCase();
      }
      if (process.platform === "darwin" && kind === "exe") {
        const result = execCapture("lsof", ["-a", "-p", String(pid), "-d", "txt", "-Fn"], {
          label: "Developer API process ownership check",
        });
        return result.split(/\r?\n/).filter((line) => line.startsWith("n")).map((line) => line.slice(1).toLowerCase()).find((name) => name.endsWith("/mediarankerserver"));
      }
    } catch {}
    return undefined;
  };
  const matches = [];
  for (const line of listing.split(/\r?\n/)) {
    const match = line.trim().match(/^(\d+)\s+(\S+)\s+(.*)$/);
    if (!match) continue;
    const [, pid, command, args] = match;
    const lower = `${command} ${args}`.toLowerCase();
    const dotnetRun = /\bdotnet\s+(run|watch)\b/.test(lower);
    const apiAssembly = lower.includes("mediarankerserver.dll");
    const appHost =
      path.basename(command).replace(/\.exe$/i, "").toLowerCase() === "mediarankerserver" ||
      /(?:^|[\\/])mediarankerserver(?:\.exe)?(?=["'\s]|$)/i.test(args);
    if (!(dotnetRun || apiAssembly || appHost)) continue;
    if (lower.includes(backendNeedle)) {
      matches.push(`${pid} ${command}`);
      continue;
    }
    const cwd = inspectProcessPath(pid, "cwd");
    const executable = inspectProcessPath(pid, "exe");
    if (!cwd || (appHost && !executable)) {
      fail(`Cannot establish ownership for candidate .NET process ${pid} (${command}); process cwd/executable inspection is unavailable. Stop or inspect that process, then rerun E2E.`);
    }
    const inCheckout = cwd === backendNeedle || cwd.startsWith(`${backendNeedle}${path.sep}`);
    const executableInBuild = executable?.startsWith(`${path.join(backend, "bin").toLowerCase()}${path.sep}`);
    if (inCheckout || executableInBuild) matches.push(`${pid} ${command}`);
  }
  if (matches.length) {
    fail(
      `A .NET process from this checkout may own shared bin/obj output. Stop it before E2E; no process was stopped. Matching process IDs/names: ${matches.join(" | ")}`,
    );
  }
}

function apiChildEnvironment(databaseUrl, apiUrl, frontendUrl) {
  return {
    ...process.env,
    DOTNET_ENVIRONMENT: "Development",
    ASPNETCORE_ENVIRONMENT: "Development",
    ASPNETCORE_URLS: apiUrl,
    ConnectionStrings__DefaultConnection: databaseUrl,
    Cors__AllowedOrigins__0: frontendUrl,
    LocalTestAuth__Enabled: "true",
    AWS__Region: "us-east-1",
    AWS__AccessKey: "e2e-inert-access-key",
    AWS__SecretKey: "e2e-inert-secret-key",
    AWS__CognitoUserPoolId: "us-east-1_e2e000000",
    AWS__CognitoClientId: "e2e-client-id",
    AWS_EC2_METADATA_DISABLED: "true",
    Media__Igdb__Enabled: "false",
    Media__Igdb__ImportEnabled: "false",
    Media__Igdb__ArtworkEnabled: "false",
    Media__Igdb__ClientId: "",
    Media__Igdb__ClientSecret: "",
    Media__Tmdb__Enabled: "false",
    Media__Tmdb__ReadAccessToken: "",
    Media__ImdbImport__Enabled: "false",
    Media__Bootstrap__Provider: "none",
    Files__Cleanup__Enabled: "false",
  };
}

async function waitHttp(url, headers = {}) {
  try {
    const response = await fetch(url, {
      headers,
      signal: AbortSignal.timeout(3000),
      redirect: "manual",
    });
    return response;
  } catch {
    return undefined;
  }
}

async function invokePlaywright(project, env) {
  assertRunning();
  const passThrough = [];
  const requested = process.argv.slice(3).filter((arg) => arg !== "--");
  for (let index = 0; index < requested.length; index += 1) {
    const arg = requested[index];
    if (arg === "--list") {
      passThrough.push(arg);
      continue;
    }
    if (arg === "--grep" || arg === "-g" || arg === "--grep-invert") {
      const value = requested[index + 1];
      if (!value || value.startsWith("--"))
        fail(`Expected a pattern after ${arg}.`);
      passThrough.push(arg, value);
      index += 1;
      continue;
    }
    if (/^--grep(?:-invert)?=.+/.test(arg)) {
      passThrough.push(arg);
      continue;
    }
    fail(
      `Unsupported Playwright option '${arg}'. This runner fixes the project, worker count, retries, and resource lifecycle.`,
    );
  }
  const cli = path.join(
    frontend,
    "node_modules",
    "@playwright",
    "test",
    "cli.js",
  );
  await stat(cli).catch(() =>
    fail(
      "Playwright is missing. Run `pnpm install` and `pnpm exec playwright install chromium`.",
    ),
  );
  const child = await spawnOwned(
    "playwright",
    process.execPath,
    [cli, "test", "--project", project, ...passThrough],
    { cwd: frontend, env },
  );
  const code = await waitExitWithDeadline(child, 20 * 60_000, "playwright");
  if (code !== 0)
    fail(
      `Playwright project '${project}' failed with exit code ${code}. See ${runDir}.`,
    );
}

async function startFrontend(frontendUrl, apiUrl, project) {
  assertRunning();
  const lock = path.join(frontend, ".next", "dev", "lock");
  if (
    await stat(lock).then(
      () => true,
      () => false,
    )
  ) {
    fail(
      `Refusing to start Next.js because ${lock} exists. Stop the developer server and remove only its stale lock after confirming it is stopped.`,
    );
  }
  const nextCli = path.join(
    frontend,
    "node_modules",
    "next",
    "dist",
    "bin",
    "next",
  );
  const env = {
    ...process.env,
    NODE_ENV: "development",
    NEXT_PUBLIC_ENABLE_LOCAL_TEST_LOGIN: "true",
    NEXT_PUBLIC_API_URL: apiUrl,
    NEXT_PUBLIC_COGNITO_USER_POOL_ID: "us-east-1_e2e000000",
    NEXT_PUBLIC_COGNITO_CLIENT_ID: "e2e-client-id",
    E2E_API_URL: apiUrl,
    E2E_BASE_URL: frontendUrl,
    E2E_RUN_DIR: runDir,
  };
  const frontendPort = Number(new URL(frontendUrl).port);
  await assertPortAvailable("Next.js", "127.0.0.1", frontendPort);
  const child = await spawnOwned(
    "next",
    process.execPath,
    [
      nextCli,
      "dev",
      "--webpack",
      "--hostname",
      "127.0.0.1",
      "--port",
      new URL(frontendUrl).port,
    ],
    { cwd: frontend, env },
  );
  const readyLine = /ready in/i;
  await waitFor(
    "next",
    async () => {
      const ownLog = await readOwnedLog("next");
      if (!readyLine.test(ownLog)) return false;
      const response = await waitHttp(frontendUrl);
      if (response && response.status >= 500) {
        const detail = ownLog.slice(-4000).trim();
        fail(`Next.js returned HTTP ${response.status} after its own startup log reported readiness. This usually indicates a route compile/runtime error${detail ? `:\n${detail}` : "."} See ${runFile("next.log")}.`);
      }
      return response && response.status < 500;
    },
    child,
  );
  await log(`Next.js ready at ${frontendUrl}; project=${project}.`);
  return { child, env };
}

async function startE2eStack() {
  assertRunning();
  ensureNoDeveloperDotnetProcess();
  commandExists("dotnet", ["--version"], "The .NET SDK check");
  commandExists("dotnet", ["ef", "--version"], "The dotnet-ef check");
  commandExists(
    "docker",
    ["version", "--format", "{{.Server.Version}}"],
    "Docker daemon check",
  );
  commandExists("docker", ["compose", "version"], "Docker Compose check");

  const apiPort = await reservePort();
  const frontendPort = await reservePort();
  portOwners.push({ label: "API", host: "127.0.0.1", port: apiPort });
  portOwners.push({ label: "Next.js", host: "127.0.0.1", port: frontendPort });
  const apiUrl = `http://127.0.0.1:${apiPort}`;
  const frontendUrl = `http://127.0.0.1:${frontendPort}`;
  const nextLock = path.join(frontend, ".next", "dev", "lock");
  if (
    await stat(nextLock).then(
      () => true,
      () => false,
    )
  )
    fail(`Refusing to start Next.js because ${nextLock} exists.`);

  dockerProject = `mr-e2e-${runId.toLowerCase().replaceAll(/[^a-z0-9-]/g, "-")}`;
  ownsDocker = true;
  await log(
    `Owned Docker project: ${dockerProject}. Exact orphan cleanup: docker compose -f "${composeFile}" -p ${dockerProject} down --volumes --remove-orphans.`,
  );
  await runOwnedCommand(
    "compose-up",
    "docker",
    ["compose", "-f", composeFile, "-p", dockerProject, "up", "-d"],
    { cwd: frontend, timeoutMs: 5 * 60_000 },
  );
  assertRunning();
  const containerId = execCapture(
    "docker",
    ["compose", "-f", composeFile, "-p", dockerProject, "ps", "-q", "postgres"],
    { label: "Finding owned PostgreSQL container" },
  ).trim();
  if (!containerId)
    fail(
      "Docker Compose did not return the run-scoped PostgreSQL container ID.",
    );
  await waitFor(
    "postgres",
    async () => {
      const status = execCapture(
        "docker",
        ["inspect", "--format", "{{.State.Health.Status}}", containerId],
        { label: "PostgreSQL health check" },
      ).trim();
      return status === "healthy";
    },
    undefined,
    90_000,
  );

  const published = execCapture(
    "docker",
    [
      "compose",
      "-f",
      composeFile,
      "-p",
      dockerProject,
      "port",
      "postgres",
      "5432",
    ],
    { label: "Reading PostgreSQL port" },
  )
    .trim()
    .split(/\r?\n/)[0];
  const match = published.match(
    /(?:\[?::\]?|127\.0\.0\.1|0\.0\.0\.0):(?<port>\d+)$/,
  );
  if (!match)
    fail(`Could not parse the owned PostgreSQL host port: ${published}`);
  const databasePort = Number(match.groups.port);
  await waitFor(
    "postgres TCP",
    () => portOpen("127.0.0.1", databasePort),
    undefined,
    15_000,
  );
  const connection = `Host=127.0.0.1;Port=${databasePort};Database=mediarank_e2e;Username=e2e;Password=e2e-local-only;Timeout=15;Command Timeout=60;Include Error Detail=true`;

  const migrationEnv = apiChildEnvironment(connection, apiUrl, frontendUrl);
  await log(
    "Applying the existing EF migration chain to this run's PostgreSQL endpoint.",
  );
  const apiWorkingDir = path.join(runDir, "api-working-directory");
  await mkdir(apiWorkingDir, { recursive: true });
  await runOwnedCommand(
    "migration",
    "dotnet",
    [
      "ef",
      "database",
      "update",
      "--project",
      projectFile,
      "--startup-project",
      projectFile,
      "--connection",
      connection,
      "--",
      "--contentRoot",
      backend,
    ],
    {
      cwd: apiWorkingDir,
      env: migrationEnv,
      timeoutMs: 5 * 60_000,
    },
  );
  assertRunning();

  const apiEnv = apiChildEnvironment(connection, apiUrl, frontendUrl);
  await assertPortAvailable("API", "127.0.0.1", apiPort);
  const api = await spawnOwned(
    "api",
    "dotnet",
    [
      "run",
      "--project",
      projectFile,
      "--no-launch-profile",
      `--property:RunWorkingDirectory=${apiWorkingDir}`,
      "--",
      "--urls",
      apiUrl,
      "--contentRoot",
      backend,
    ],
    { cwd: backend, env: apiEnv },
  );
  await waitFor(
    "api",
    async () => {
      const ownLog = await readOwnedLog("api");
      if (!ownLog.toLowerCase().includes(`now listening on: ${apiUrl}`.toLowerCase())) return false;
      if (!(await portOpen("127.0.0.1", apiPort))) return false;
      const response = await waitHttp(`${apiUrl}/api/Templates`, {
        Authorization: "Bearer MediaRankerLocalTest",
      });
      return response?.status === 200;
    },
    api,
    120_000,
  );
  const logFiles = await readdir(path.join(apiWorkingDir, "logs")).catch(
    () => [],
  );
  if (!logFiles.some((file) => file.startsWith("app-log-"))) {
    fail(
      `API became ready but its Serilog file was not found under the owned working directory ${apiWorkingDir}.`,
    );
  }
  await log(
    `API authenticated read succeeded at ${apiUrl}; Serilog output is in ${path.join(apiWorkingDir, "logs")}.`,
  );
  return { apiUrl, frontendUrl, frontendPort };
}

async function checkPortsReleased() {
  for (const { label, host, port } of portOwners) {
    if (await portOpen(host, port)) {
      await log(
        `Cleanup warning: ${label} still accepts TCP connections on ${host}:${port}.`,
      );
      signalCode ??= 1;
    }
  }
}

async function killOwned(child) {
  if (!child) return;
  if (child.spawnError && child.pid === undefined) {
    processes.delete(child);
    await log(`Owned child ${child.pid ?? "without PID"} failed to spawn; no process tree was created.`);
    return true;
  }
  if (child.exitCode !== null || child.signalCode !== null) {
    processes.delete(child);
    return false;
  }

  const observedExit = () => child.exitCode !== null || child.signalCode !== null;
  const waitForChildExit = (timeoutMs) =>
    new Promise((resolve) => {
      if (observedExit()) return resolve(true);
      let timer;
      const finish = (exited) => {
        clearTimeout(timer);
        child.removeListener("exit", onExit);
        child.removeListener("error", onError);
        resolve(exited || observedExit());
      };
      const onExit = () => finish(true);
      const onError = () => finish(false);
      child.once("exit", onExit);
      child.once("error", onError);
      timer = setTimeout(() => finish(false), timeoutMs);
    });

  let stopError;
  if (process.platform === "win32") {
    try {
      execCapture("taskkill", ["/PID", String(child.pid), "/T", "/F"], {
        label: "Stopping owned process tree",
        timeout: 15_000,
      });
    } catch (error) {
      stopError = error;
    }
    if (!(await waitForChildExit(5000))) {
      signalCode ??= 1;
      await log(`Cleanup failed: owned child ${child.pid} is still active after taskkill${stopError ? ` (${stopError.message})` : ""}.`);
      return false;
    }
  } else {
    try {
      process.kill(-child.pid, "SIGKILL");
    } catch (error) {
      stopError = error;
    }
    if (!(await waitForChildExit(5000))) {
      signalCode ??= 1;
      await log(`Cleanup failed: owned child ${child.pid} is still active after SIGKILL${stopError ? ` (${stopError.message})` : ""}.`);
      return false;
    }
  }

  if (!observedExit()) {
    signalCode ??= 1;
    await log(`Cleanup could not confirm exit for owned child ${child.pid}; its PID remains tracked.`);
    return false;
  }
  if (stopError) {
    signalCode ??= 1;
    await log(`Cleanup signal for owned child ${child.pid} reported an error: ${stopError.message}; process exit was observed.`);
  } else {
    await log(`Observed owned child ${child.pid} exit after stopping its process tree.`);
  }
  processes.delete(child);
  return true;
}

let processStopPromise;
function stopProcesses() {
  if (processStopPromise) return processStopPromise;
  const pass = (async () => {
    for (const child of [...processes].reverse()) {
      try {
        await killOwned(child);
      } catch (error) {
        signalCode ??= 1;
        await log(`Cleanup failed for owned child ${child.pid ?? "unknown"}: ${error.message}`);
      }
    }
  })();
  processStopPromise = pass.finally(() => {
    processStopPromise = undefined;
  });
  return processStopPromise;
}

async function cleanup() {
  if (cleaning) return;
  cleaning = true;
  await stopProcesses();
  if (ownsDocker) {
    try {
      const postgresLogs = execCapture(
        "docker",
        ["compose", "-f", composeFile, "-p", dockerProject, "logs", "--no-color", "--tail", "500", "postgres"],
        { label: "Capturing owned PostgreSQL logs", timeout: 30_000, maxBuffer: 10 * 1024 * 1024 },
      );
      await writeFile(runFile("postgres.log"), postgresLogs, "utf8");
      await log(`Saved bounded PostgreSQL container logs to ${runFile("postgres.log")}.`);
    } catch (error) {
      await log(`Could not capture owned PostgreSQL logs before cleanup: ${error.message}`);
    }
    try {
      execCapture(
        "docker",
        [
          "compose",
          "-f",
          composeFile,
          "-p",
          dockerProject,
          "down",
          "--volumes",
          "--remove-orphans",
        ],
        { label: "Removing owned E2E PostgreSQL resources", timeout: 90_000 },
      );
      await log(`Removed only Docker Compose project ${dockerProject}.`);
    } catch (error) {
      signalCode ??= 1;
      await log(
        `Cleanup failed for owned Docker project ${dockerProject}: ${error.message}`,
      );
    }
  }
  await checkPortsReleased();
}

process.on("SIGINT", () => {
  signalCode = 130;
  void stopProcesses();
});
process.on("SIGTERM", () => {
  signalCode = 143;
  void stopProcesses();
});

try {
  await log(`Browser test run ${runId}; project=${mode}; artifacts=${runDir}`);
  let apiUrl;
  let frontendUrl;
  let frontendPort;
  if (mode === "e2e") {
    ({ apiUrl, frontendUrl, frontendPort } = await startE2eStack());
  } else {
    const nextPort = await reservePort();
    const apiPortUnused = await reservePort();
    frontendPort = nextPort;
    frontendUrl = `http://127.0.0.1:${nextPort}`;
    apiUrl = `http://127.0.0.1:${apiPortUnused}`;
  }
  if (mode === "browser")
    portOwners.push({
      label: "Next.js",
      host: "127.0.0.1",
      port: frontendPort,
    });
  const { env } = await startFrontend(frontendUrl, apiUrl, mode);
  await invokePlaywright(mode, env);
  await log(`Playwright project '${mode}' passed.`);
} catch (error) {
  signalCode ??= 1;
  await log(`FAILED: ${error.stack ?? error.message}`);
  console.error(
    `Browser test setup or execution failed. Logs and reports are retained at ${runDir}`,
  );
} finally {
  await cleanup();
  await log(`Run finished with exit code ${signalCode ?? 0}.`);
  await logHandle.close();
}

process.exitCode = signalCode ?? 0;
