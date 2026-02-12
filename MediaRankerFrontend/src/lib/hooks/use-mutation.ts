import { useMutation as useTanstackMutation, UseMutationOptions } from "@tanstack/react-query";


interface UseMutationOptionsType extends Omit<UseMutationOptions, 'mutationFn'> {
  route: string;
  method: 'POST' | 'PUT' | 'DELETE';
  data?: Record<string, unknown>;
}

export function useMutation(options: UseMutationOptionsType) {

  const mutation = useTanstackMutation({
    mutationFn: async () => httpMutation(options),
    ...options
  });

  return mutation;

}

const httpMutation = async (options: UseMutationOptionsType) => {
  const url = new URL(options.route, process.env.NEXT_PUBLIC_API_URL);
  const response = await fetch(url.toString(), {
    method: options.method,
    headers: {
      'Content-Type': 'application/json'
    },
    body: options.data ? JSON.stringify(options.data) : undefined
  });
  // TODO: need to determine agreed upon error handling strategy and DTOs (ie JSON/string/anything!?).
  return response;
};