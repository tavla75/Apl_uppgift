import { DatePipe } from '@angular/common';
import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { BookService } from '../../core/book.service';

@Component({
  selector: 'app-home-page',
  imports: [DatePipe, RouterLink],
  templateUrl: './home.page.html',
  styleUrl: './home-page.css',
})
export class HomePage {
  private readonly bookService = inject(BookService);
  protected books = this.bookService.list();

  protected deleteBook(bookId: number): void {
    const book = this.books.find((item) => item.id === bookId);
    if (!book || !window.confirm(`Vill du ta bort "${book.title}"?`)) {
      return;
    }

    if (this.bookService.delete(bookId)) {
      this.books = this.bookService.list();
    }
  }
}
