# MediaRanker

A personal media reviewing and ranking web application. Built as a learning project to practice **Modular Monolith** architecture and **Domain-Driven Development** patterns such as **Event-Driven Architecture** and **Eventual Consistency**.

## Screenshots (Coming Soon...)

## Why This Stack

- **ASP.NET Core (.NET 9)** — Practicing building APIs with the .NET ecosystem.
- **Next.js 16 (React 19)** — My most experienced frontend framework; using it to stay sharp and explore newer React features.
- **Modular Monolith** — Structured as feature-based modules within a single deployable to practice DDD concepts (bounded contexts, domain events, module boundaries) while using familiar technologies. Eventually I plan to extract specific modules into separate microservices.

## Architecture Overview

```
┌─────────────────────┐       ┌──────────────────────────────────┐
│  Next.js Frontend   │──────▶│  ASP.NET Core API                │
│  (React 19 / MUI)   │ REST  │  Modular Monolith (.NET 9)       │
└─────────────────────┘       │                                  │
                              │  ┌────────────┐ ┌─────────────┐  │
                              │  │ Templates  │ │   Media     │  │
                              │  └────────────┘ └─────────────┘  │
                              │  ┌────────────┐ ┌─────────────┐  │
                              │  │  Reviews   │ │   Files     │  │
                              │  └────────────┘ └─────────────┘  │
                              └──────────┬───────────┬───────────┘
                                         │           │
                              ┌──────────▼──┐  ┌─────▼──────────┐
                              │ PostgreSQL  │  │   AWS S3       │
                              │ (EF Core)   │  │ (file storage) │
                              └─────────────┘  └────────────────┘

                              ┌──────────────────────────────────┐
                              │         AWS Cognito              │
                              │  (authentication / JWT tokens)   │
                              └──────────────────────────────────┘
```

### AWS Integrations

- **Amazon Cognito** — Handles user authentication. The frontend uses AWS Amplify for sign-up/login flows; the API validates Cognito-issued JWTs via Bearer authentication.
- **Amazon S3** — Stores uploaded files (e.g. media cover images). The API generates pre-signed URLs for direct browser uploads and manages the upload lifecycle.

## Solution Structure

| Project | Description |
|---|---|
| `MediaRankerServer/` | ASP.NET Core Web API — modular monolith with feature-based modules |
| `MediaRankerFrontend/` | Next.js frontend — React 19, MUI, TanStack Query |
| `MediaRankerServer.IntegrationTests/` | Integration tests that run in docker to test PostgreSQL and AWS interactions (Testcontainers, Respawn, Localstack) |
| `MediaRankerServer.UnitTests/` | Isolated unit tests (Moq, FluentAssertions) |

## Local Development

### Prerequisites

- .NET 9 SDK
- Node.js (LTS)
- Docker (for PostgreSQL via Docker Compose)
- AWS credentials configured (Cognito + S3)

### Running
Currently the project is set up to run the API and frontend separately as I'm mid development. In the future I plan to containerize the entire application for easy deployment.

```bash
# Start PostgreSQL
docker compose up -d postgres

# Run the API (from MediaRankerServer/)
dotnet run

# Run the frontend (from MediaRankerFrontend/)
pnpm install
pnpm run dev
```
