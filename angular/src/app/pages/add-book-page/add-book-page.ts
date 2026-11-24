import {HttpErrorResponse} from '@angular/common/http';
import {Component, inject, OnInit} from '@angular/core';
import {FormsModule} from '@angular/forms';
import {ActivatedRoute, Router, RouterLink} from '@angular/router';
import {BookRequest} from '../../core/book.models';
import {BookService} from '../../core/book.service';

@Component({
  selector: 'app-add-book-page',
  imports: [FormsModule, RouterLink],
  templateUrl: './add-book-page.html',
  styleUrl: './add-book-page.css',
})
export class AddBookPage implements OnInit {
  private readonly bookService = inject(BookService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  private readonly routeBookId = this.route.snapshot.paramMap.get('id');
  private readonly bookId = this.routeBookId === null ? null : Number(this.routeBookId);

  protected readonly isEditing = this.routeBookId !== null;
  protected bookNotFound = false;
  protected error = '';

  protected title = '';
  protected author = '';
  protected publicationDate = '';

  ngOnInit(): void {
    if (this.bookId === null) {
      return;
    }

    this.bookService.get(this.bookId).subscribe({
      next: (book) => {
        this.title = book.title;
        this.author = book.author;
        this.publicationDate = book.publicationDate;
      },
      error: (error: HttpErrorResponse) => {
        this.bookNotFound = error.status === 404;
        this.error = this.bookNotFound
          ? 'Boken hittades inte.'
          : 'Kunde inte hämta boken.';
        console.error(error);
      },
    });
  }

  protected save(): void {
    const request: BookRequest = {
      title: this.title.trim(),
      author: this.author.trim(),
      publicationDate: this.publicationDate.trim(),
    };

    if (!request.title || !request.author || !request.publicationDate) {
      return;
    }

    const operation =
      this.bookId === null
        ? this.bookService.create(request)
        : this.bookService.update(this.bookId, request);

    operation.subscribe({
      next: () => {
        void this.router.navigateByUrl('/home-page');
      },
      error: (error: HttpErrorResponse) => {
        this.error = 'Kunde inte spara boken.';
        console.error(error);
      },
    });
  }
}
