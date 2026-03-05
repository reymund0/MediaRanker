export interface ProblemDetails {
  type: string;
  title: string;
  status: number;
  detail?: string;
  instance?: string;
  [key: string]: unknown;
}

export class ProblemDetailsError extends Error implements ProblemDetails {
  public type: string;
  public title: string;
  public status: number;
  public detail?: string;
  public instance?: string;
  // Index signature to support extension members
  [key: string]: unknown;

  constructor(problemDetails: ProblemDetails) {
    // Call the parent Error constructor with the detail or title as the message
    super(problemDetails.detail || problemDetails.title);

    // Explicitly set the prototype chain to ensure instanceof works correctly in some environments
    // (especially older Node.js/TypeScript versions)
    Object.setPrototypeOf(this, ProblemDetailsError.prototype);

    // Copy all properties from the provided problemDetails object to the error instance
    this.type = problemDetails.type;
    this.title = problemDetails.title;
    this.status = problemDetails.status;
    this.detail = problemDetails.detail;
    this.instance = problemDetails.instance;

    // Set the name of the error for better identification
    this.name = "ProblemDetailsError";

    // User-friendly message to display.
    this.message = problemDetails.detail || problemDetails.title;
  }
}
