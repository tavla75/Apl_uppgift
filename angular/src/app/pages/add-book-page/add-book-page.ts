import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { BookService } from '../../core/book.service';

@Component({
  selector: 'app-add-book-page',
  imports: [FormsModule, RouterLink],
  templateUrl: './add-book-page.html',
  styleUrl: './add-book-page.css',
})
export class AddBookPage {
  private readonly bookService = inject(BookService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly routeBookId = this.route.snapshot.paramMap.get('id');
  private readonly bookId = this.routeBookId === null ? null : Number(this.routeBookId);
  private readonly existingBook =
    this.bookId === null ? undefined : this.bookService.get(this.bookId);

  protected readonly isEditing = this.routeBookId !== null;
  protected readonly bookNotFound = this.isEditing && !this.existingBook;

  protected title = this.existingBook?.title ?? '';
  protected author = this.existingBook?.author ?? '';
  protected publicationDate = this.existingBook?.publicationDate ?? '';

  protected save(): void {
    const title = this.title.trim();
    const author = this.author.trim();
    if (!title || !author || !this.publicationDate) {
      return;
    }

    const book = {
      title,
      author,
      publicationDate: this.publicationDate,
    };

    if (this.bookId === null) {
      this.bookService.add(book);
    } else if (!this.bookService.update(this.bookId, book)) {
      return;
    }

    void this.router.navigateByUrl('/home-page');
  }
}
