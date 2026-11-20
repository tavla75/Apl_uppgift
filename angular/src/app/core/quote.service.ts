import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { Quote, QuoteRequest } from './quote.models';

@Injectable({ providedIn: 'root' })
export class QuoteService {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/quotes';

  list(): Observable<Quote[]> {
    return this.http.get<Quote[]>(this.url);
  }

  create(request: QuoteRequest): Observable<Quote> {
    return this.http.post<Quote>(this.url, request);
  }

  update(id: number, request: QuoteRequest): Observable<Quote> {
    return this.http.put<Quote>(`${this.url}/${id}`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
