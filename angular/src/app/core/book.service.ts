import { Injectable } from '@angular/core';
import { Book } from './book.models';

const BOOKS_STORAGE_KEY = 'apl-uppgift.books';

@Injectable({ providedIn: 'root' })
export class BookService {
  private books = this.loadBooks();
  private nextId = Math.max(0, ...this.books.map((book) => book.id)) + 1;

  list(): Book[] {
    return [...this.books];
  }

  get(id: number): Book | undefined {
    return this.books.find((book) => book.id === id);
  }

  add(book: Omit<Book, 'id'>): void {
    this.saveBooks([...this.books, { ...book, id: this.nextId }]);
    this.nextId++;
  }

  update(id: number, changes: Omit<Book, 'id'>): boolean {
    const index = this.books.findIndex((book) => book.id === id);
    if (index === -1) {
      return false;
    }

    this.saveBooks(this.books.map((book) => (book.id === id ? { ...changes, id } : book)));
    return true;
  }

  delete(id: number): boolean {
    const remainingBooks = this.books.filter((book) => book.id !== id);
    if (remainingBooks.length === this.books.length) {
      return false;
    }

    this.saveBooks(remainingBooks);
    return true;
  }

  private loadBooks(): Book[] {
    const storedBooks = localStorage.getItem(BOOKS_STORAGE_KEY);
    if (storedBooks === null) {
      return [
        { id: 1, title: 'The Hobbit', author: 'J.R.R Tolkien', publicationDate: '1937' },
        { id: 2, title: 'Pride and Prejudice', author: 'Jane Austen', publicationDate: '1813' },
        { id: 3, title: 'Mio, My Son', author: 'Astrid Lindgren', publicationDate: '1954' },
      ];
    }

    const books: unknown = JSON.parse(storedBooks);
    if (
      !Array.isArray(books) ||
      !books.every(
        (book): book is Book =>
          typeof book === 'object' &&
          book !== null &&
          typeof book.id === 'number' &&
          Number.isInteger(book.id) &&
          typeof book.title === 'string' &&
          typeof book.author === 'string' &&
          typeof book.publicationDate === 'string',
      )
    ) {
      throw new Error(
        'Saved books are invalid. Clear the apl-uppgift.books browser storage entry to reset them.',
      );
    }

    return books;
  }

  private saveBooks(books: Book[]): void {
    localStorage.setItem(BOOKS_STORAGE_KEY, JSON.stringify(books));
    this.books = books;
  }
}
