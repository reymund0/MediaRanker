/** Round to the nearest integer, resolving exact halves to the nearest even integer. */
export function roundToEven(value) {
  const lower = Math.floor(value);
  const fraction = value - lower;

  if (fraction < 0.5) return lower;
  if (fraction > 0.5) return lower + 1;
  return lower % 2 === 0 ? lower : lower + 1;
}
