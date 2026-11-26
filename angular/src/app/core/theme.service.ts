import { Injectable, signal, effect, inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser} from '@angular/common';

@Injectable({
  providedIn: 'root'
})
export class ThemeService {
  private platformId = inject(PLATFORM_ID);

  readonly isDarkMode = signal(false);

  constructor(){
    if (isPlatformBrowser(this.platformId)) {
      const savedTheme = localStorage.getItem('theme');
      const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches;

      this.isDarkMode.set(savedTheme ? savedTheme === 'dark' : prefersDark);

      effect(() => {
        const theme = this.isDarkMode() ? 'dark' : 'light';
        document.documentElement.setAttribute('data-bs-theme', theme);
        localStorage.setItem('theme', theme);
      });

    }
  }

  toggleTheme(){
    this.isDarkMode.update(dark => !dark);
  }
}


