import { useParams } from "next/navigation";

/*
Page is going to have the standard input boxes for the title and description.
The cool part is at the top, on the left hand side we will show the media title, type, and image.
To the right of the media info, will be the template fields which will be scored by a cool 5 star rating system.
*/
export default function ReviewEditPage() {
  const params = useParams();
  const id = params.id;
  return <div>Review Edit Page {id}</div>;
}
