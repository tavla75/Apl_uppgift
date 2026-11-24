import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Quote, QuoteRequest } from '../../core/quote.models';
import { QuoteService } from '../../core/quote.service';

interface SciFiFavorite {
  id: string;
  text: string;
  film: string;
}

const SCI_FI_FAVORITES_KEY = 'apl-uppgift.sci-fi-favorites';
const DEFAULT_SCI_FI_FAVORITES: SciFiFavorite[] = [
  { id: 'star-wars', text: 'Må Kraften vara med dig.', film: 'Star Wars' },
  {
    id: 'the-matrix',
    text: 'Frihet börjar med modet att ifrågasätta den verklighet vi tar för given.',
    film: 'The Matrix',
  },
  {
    id: 'arrival',
    text: 'Tålamod och kommunikation kan förvandla mötet med det okända.',
    film: 'Arrival',
  },
  {
    id: 'blade-runner-2049',
    text: 'Vår betydelse avgörs inte av vårt ursprung, utan av våra val.',
    film: 'Blade Runner 2049',
  },
  {
    id: '2001-a-space-odyssey',
    text: 'Utforskandet av rymden får oss också att fundera över människans plats i universum.',
    film: '2001: A Space Odyssey',
  },
];

@Component({
  selector: 'app-quotes-page',
  imports: [FormsModule],
  templateUrl: './quotes-page.html',
  styleUrl: './quotes-page.css',
})
export class QuotesPage implements OnInit {
  private readonly quoteService = inject(QuoteService);
  protected sciFiFavorites = this.loadSciFiFavorites();
  protected quotes: Quote[] = [];
  protected text = '';
  protected author = '';
  protected editingId: number | null = null;
  protected editingFavoriteId: string | null = null;
  protected loading = true;
  protected submitting = false;
  protected error = '';

  ngOnInit(): void {
    this.loadQuotes();
  }

  protected save(): void {
    const text = this.text.trim();
    const author = this.author.trim();
    if (this.editingFavoriteId !== null) {
      this.sciFiFavorites = this.sciFiFavorites.map((favorite) =>
        favorite.id === this.editingFavoriteId ? { ...favorite, text, film: author } : favorite,
      );
      this.saveSciFiFavorites();
      this.resetForm();
      return;
    }

    const request: QuoteRequest = {
      text,
      author: author || null,
    };
    this.error = '';
    this.submitting = true;
    const operation =
      this.editingId === null
        ? this.quoteService.create(request)
        : this.quoteService.update(this.editingId, request);

    operation.subscribe({
      next: () => {
        this.resetForm();
        this.submitting = false;
        this.loadQuotes();
      },
      error: (response: HttpErrorResponse) => this.showError(response),
    });
  }

  protected edit(quote: Quote): void {
    this.editingId = quote.id;
    this.editingFavoriteId = null;
    this.text = quote.text;
    this.author = quote.author ?? '';
    this.error = '';
  }

  protected editFavorite(favorite: SciFiFavorite): void {
    this.editingId = null;
    this.editingFavoriteId = favorite.id;
    this.text = favorite.text;
    this.author = favorite.film;
    this.error = '';
  }

  protected cancelEdit(): void {
    this.resetForm();
  }

  protected delete(quote: Quote): void {
    this.error = '';
    this.quoteService.delete(quote.id).subscribe({
      next: () => (this.quotes = this.quotes.filter((item) => item.id !== quote.id)),
      error: (response: HttpErrorResponse) => this.showError(response),
    });
  }

  protected deleteFavorite(favorite: SciFiFavorite): void {
    this.sciFiFavorites = this.sciFiFavorites.filter((item) => item.id !== favorite.id);
    this.saveSciFiFavorites();
    if (this.editingFavoriteId === favorite.id) {
      this.resetForm();
    }
  }

  private loadQuotes(): void {
    this.loading = true;
    this.quoteService.list().subscribe({
      next: (quotes) => {
        this.quotes = quotes;
        this.loading = false;
      },
      error: (response: HttpErrorResponse) => this.showError(response),
    });
  }

  private resetForm(): void {
    this.text = '';
    this.author = '';
    this.editingId = null;
    this.editingFavoriteId = null;
  }

  private loadSciFiFavorites(): SciFiFavorite[] {
    const savedFavorites = localStorage.getItem(SCI_FI_FAVORITES_KEY);
    if (savedFavorites === null) {
      return DEFAULT_SCI_FI_FAVORITES.map((favorite) => ({ ...favorite }));
    }

    const favorites: unknown = JSON.parse(savedFavorites);
    if (
      !Array.isArray(favorites) ||
      !favorites.every(
        (favorite): favorite is SciFiFavorite =>
          typeof favorite === 'object' &&
          favorite !== null &&
          typeof favorite.id === 'string' &&
          typeof favorite.text === 'string' &&
          typeof favorite.film === 'string',
      )
    ) {
      throw new Error(
        'Saved sci-fi favorites are invalid. Clear the apl-uppgift.sci-fi-favorites browser storage entry to reset them.',
      );
    }

    return favorites;
  }

  private saveSciFiFavorites(): void {
    localStorage.setItem(SCI_FI_FAVORITES_KEY, JSON.stringify(this.sciFiFavorites));
  }

  private showError(response: HttpErrorResponse): void {
    this.submitting = false;
    this.loading = false;
    this.error =
      response.error?.detail ??
      response.error?.title ??
      'Det gick inte att spara ändringen. Försök igen.';
  }
}
