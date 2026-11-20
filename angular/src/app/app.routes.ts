import { Routes } from '@angular/router';
import { authGuard } from './core/auth.guard';
import { AuthPage } from './pages/auth-page/auth-page';
import { HomePage } from './pages/home-page/home-page';
import { QuotesPage } from './pages/quotes-page/quotes-page';

export const routes: Routes = [
  { path: 'login', component: AuthPage },
  { path: 'register', component: AuthPage },
  { path: 'home-page', component: HomePage, canActivate: [authGuard] },
  { path: 'quotes', component: QuotesPage, canActivate: [authGuard] },
  { path: '', pathMatch: 'full', redirectTo: 'login' },
  { path: '**', redirectTo: 'login' },
];
