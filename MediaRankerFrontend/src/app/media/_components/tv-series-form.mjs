export function buildSeriesUpsertRequest({ id, title, startYear }) {
  return {
    id: id ?? null,
    title: title.trim(),
    collectionType: 0,
    mediaType: "TvShow",
    parentMediaCollectionId: null,
    releaseDate: `${startYear}-01-01`,
  };
}
