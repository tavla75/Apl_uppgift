import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { Book, BookRequest } from './book.models';

@Injectable({ providedIn: 'root' })
export class BookService {
  private readonly http = inject(HttpClient);
  private readonly url = '/api/books';


  list(): Observable<Book[]>{
    return this.http.get<Book[]>(this.url);
  }
  get(id: number): Observable<Book>{
    return this.http.get<Book>(`${this.url}/${id}`);
  }

  create(request: BookRequest): Observable<Book>{
  return this.http.post<Book>(this.url, request);
  }

  update(id: number, request: BookRequest): Observable<Book>{
    return this.http.put<Book>(`${this.url}/${id}`, request);
  }

  delete(id: number): Observable<void>{
    return this.http.delete<void>(`${this.url}/${id}`);
  }
}
