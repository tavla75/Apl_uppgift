import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Quote, QuoteRequest } from '../../core/quote.models';
import { QuoteService } from '../../core/quote.service';

@Component({
  selector: 'app-quotes-page',
  imports: [FormsModule],
  templateUrl: './quotes-page.html',
  styleUrl: './quotes-page.css',
})
export class QuotesPage implements OnInit {
  private readonly quoteService = inject(QuoteService);
  protected readonly sciFiFavorites = [
    'Rymden är inte tyst; den väntar bara på rätt fråga.',
    'Varje ny värld börjar som en punkt på en karta.',
    'Framtiden är ett frö vi skickar genom mörkret.',
    'Teknik kan visa vägen, men modet måste ta steget.',
    'Bland stjärnorna är hemmet fortfarande det vi bygger tillsammans.',
  ];
  protected quotes: Quote[] = [];
  protected text = '';
  protected author = '';
  protected editingId: number | null = null;
  protected loading = true;
  protected submitting = false;
  protected error = '';

  ngOnInit(): void {
    this.loadQuotes();
  }

  protected save(): void {
    const request: QuoteRequest = {
      text: this.text.trim(),
      author: this.author.trim() || null,
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
    this.text = quote.text;
    this.author = quote.author ?? '';
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
