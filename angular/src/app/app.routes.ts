import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { AuthPage } from './pages/auth-page/auth-page';
import { AddBookPage } from './pages/add-book-page/add-book-page';
import { HomePage } from './pages/home-page/home-page';

export const routes: Routes = [
  { path: 'login', component: AuthPage },
  { path: 'register', component: AuthPage },
  { path: 'home-page', component: HomePage, canActivate: [authGuard] },
  { path: 'books/new', component: AddBookPage, canActivate: [authGuard] },
  { path: 'books/:id/edit', component: AddBookPage, canActivate: [authGuard] },
  {
    path: 'quotes',
    loadComponent: () =>
      import('./pages/quotes-page/quotes-page').then((module) => module.QuotesPage),
    canActivate: [authGuard],
  },
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: '**', redirectTo: 'login' },
];
