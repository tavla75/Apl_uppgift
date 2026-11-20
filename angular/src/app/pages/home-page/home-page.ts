import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

interface Book {
  id: number;
  title: string;
  author: string;
  year: number;
}

@Component({
  selector: 'app-home-page',
  imports: [FormsModule, RouterLink],
  templateUrl: './home.page.html',
  styleUrl: './home-page.css',
})
export class HomePage {
  protected books: Book[] = [
    { id: 1, title: 'The Hobbit', author: 'J.R.R Tolkien', year: 1937 },
    { id: 2, title: 'Pride and Prejudice', author: 'Jane Austen', year: 1813 },
    { id: 3, title: 'Mio, My Son', author: 'Astrid Lindgren', year: 1954 },
  ];
  protected title = '';
  protected author = '';
  protected year: number | null = null;

  protected addBook(): void {
    const title = this.title.trim();
    const author = this.author.trim();

    if (!title || !author || this.year === null || !Number.isInteger(this.year)) {
      return;
    }

    this.books = [
      ...this.books,
      {
        id: Math.max(0, ...this.books.map((book) => book.id)) + 1,
        title,
        author,
        year: this.year,
      },
    ];
    this.title = '';
    this.author = '';
    this.year = null;
  }
}
