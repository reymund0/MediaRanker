export const TV_EPISODE_PAGE_SIZE = 25;

export function mergeEpisodePages(previous, next) {
  const merged = new Map(previous.map((item) => [item.id, item]));
  next.forEach((item) => merged.set(item.id, item));
  return [...merged.values()];
}
