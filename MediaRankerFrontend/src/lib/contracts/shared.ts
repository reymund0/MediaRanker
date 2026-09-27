// If this grows too large, split into module-based files.

export interface PageResult<T> {
  items: T[];
  totalCount?: number;
  page: number;
  pageSize: number;
}

export interface PageRequest {
  page?: number;
  pageSize?: number;
  sortField?: string;
  sortDirection?: "asc" | "desc";
  searchTerm?: string;
  searchField?: string;
  includeTotalCount?: boolean;
}

export enum MediaType {
  VideoGame = "VideoGame",
  Book = "Book",
  Movie = "Movie",
  TvShow = "TvShow",
  Album = "Album",
  Concert = "Concert",
}

export const MEDIA_TYPE_LABELS: Record<MediaType, string> = {
  [MediaType.VideoGame]: "Video Game",
  [MediaType.Book]: "Book",
  [MediaType.Movie]: "Movie",
  [MediaType.TvShow]: "TV Show",
  [MediaType.Album]: "Album",
  [MediaType.Concert]: "Concert",
};

export const ALL_MEDIA_TYPES: MediaType[] = Object.values(MediaType);

export interface TemplateFieldDto {
  id: number;
  name: string;
  position: number;
}

export interface TemplateDto {
  id: number;
  isSystem: boolean;
  userId: string;
  name: string;
  description: string | null;
  createdAt: string;
  updatedAt: string;
  fields: TemplateFieldDto[];
  mediaType: MediaType;
}
