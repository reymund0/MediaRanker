import { parseISO } from "date-fns";
import { MediaDto } from "./contracts";

export type MediaRow = Omit<
  MediaDto,
  "id" | "releaseDate" | "createdAt" | "updatedAt"
> & {
  id: number | undefined;
  releaseDate: Date | null;
  createdAt: Date | null;
  updatedAt: Date | null;
};

export const mapMediaToRow = (media: MediaDto): MediaRow => ({
  ...media,
  releaseDate: media.releaseDate ? parseISO(media.releaseDate) : null,
  createdAt: media.createdAt ? parseISO(media.createdAt) : null,
  updatedAt: media.updatedAt ? parseISO(media.updatedAt) : null,
});
