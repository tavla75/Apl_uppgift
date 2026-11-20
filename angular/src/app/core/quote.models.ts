export interface Quote {
  id: number;
  text: string;
  author: string | null;
  createdAtUtc: string;
}

export interface QuoteRequest {
  text: string;
  author: string | null;
}
