import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Book } from '../../core/book.models';
import { BookService } from '../../core/book.service';

@Component({
  selector: 'app-home-page',
  imports: [DatePipe, RouterLink],
  templateUrl: './home.page.html',
  styleUrl: './home-page.css',
})
export class HomePage implements OnInit {
  private readonly bookService = inject(BookService);

  protected books: Book[] = [];
  protected error = '';

  ngOnInit(): void {
    this.bookService.list().subscribe({
      next: data => {
        this.books = data;
      },
      error: (error: HttpErrorResponse) => {
        this.error = 'Kunde inte hämta böckerna';
        console.error(error);
      },
    });
  }

  protected deleteBook(bookId: number): void {
    const book = this.books.find(item => item.id === bookId);
    if (!book || !window.confirm(`Vill du ta bort "${book.title}"?`)){
      return;
    }
    this.bookService.delete(bookId).subscribe({
      next: () => {
        this.books = this.books.filter((item) => item.id !== bookId);
      },
      error: (error: HttpErrorResponse) => {
        this.error = 'Kunde inte tabort boken';
        console.error(error);
      },
    })
  }
}

