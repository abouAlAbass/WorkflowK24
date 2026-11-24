import { useEffect, useState, useCallback } from 'react';
import type { SupportedLanguage, Direction } from '../i18n/translations';
import { translations } from '../i18n/translations';

export type Theme = 'light' | 'dark';
export type DesignTheme = 'corporate' | 'microsoft' | 'apple' | 'sap';

export interface DesignThemeOption {
  id: DesignTheme;
  name: string;
  brand: string;
  fontFamily: string;
  fontLabel: string;
  lightPrimary: string;
  darkPrimary: string;
  description: string;
}

export const DESIGN_THEMES: DesignThemeOption[] = [
  {
    id: 'corporate',
    name: 'Corporate Enterprise',
    brand: 'Royal Blue',
    fontFamily: "'Plus Jakarta Sans', 'Inter', system-ui, sans-serif",
    fontLabel: 'Plus Jakarta Sans',
    lightPrimary: '#2563EB',
    darkPrimary: '#3B82F6',
    description: 'Executive B2B Enterprise BPM with royal blue accents and balanced curves.'
  },
  {
    id: 'microsoft',
    name: 'Microsoft Fluent',
    brand: 'Fluent Cyan-Blue',
    fontFamily: "'Segoe UI Variable', 'Segoe UI', 'Open Sans', system-ui, sans-serif",
    fontLabel: 'Segoe UI',
    lightPrimary: '#0078D4',
    darkPrimary: '#00BCF2',
    description: 'Windows 11 & Microsoft 365 Fluent Design with sharp 6px geometry and Segoe UI.'
  },
  {
    id: 'apple',
    name: 'Apple Human Interface',
    brand: 'Cupertino Violet',
    fontFamily: "-apple-system, BlinkMacSystemFont, 'SF Pro Display', 'Outfit', sans-serif",
    fontLabel: 'SF Pro / Outfit',
    lightPrimary: '#5856D6',
    darkPrimary: '#AF52DE',
    description: 'macOS Sequoia & iOS HIG aesthetic with Cupertino violet accents and 20px squircles.'
  },
  {
    id: 'sap',
    name: 'SAP Horizon / Fiori',
    brand: 'SAP Horizon Gold',
    fontFamily: "'72', 'Source Sans 3', 'Roboto', sans-serif",
    fontLabel: 'SAP 72 / Source Sans',
    lightPrimary: '#E9730C',
    darkPrimary: '#FF9A3C',
    description: 'SAP Fiori & Horizon standard with signature gold/amber highlights and compact panels.'
  }
];

const THEME_KEY = 'tamam_theme';
const DESIGN_THEME_KEY = 'tamam_design_theme';
const LANG_KEY = 'tamam_language';
const DIR_KEY = 'tamam_direction';

let globalTheme: Theme = (() => {
  try {
    const stored = localStorage.getItem(THEME_KEY);
    if (stored === 'dark' || stored === 'light') return stored;
  } catch {
    // ignore
  }
  return typeof window !== 'undefined' && typeof window.matchMedia === 'function' && window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
})();

let globalDesignTheme: DesignTheme = (() => {
  try {
    const stored = localStorage.getItem(DESIGN_THEME_KEY);
    if (stored === 'corporate' || stored === 'microsoft' || stored === 'apple' || stored === 'sap') {
      return stored as DesignTheme;
    }
  } catch {
    // ignore
  }
  return 'corporate';
})();

let globalLanguage: SupportedLanguage = (() => {
  try {
    const stored = localStorage.getItem(LANG_KEY);
    if (stored === 'en' || stored === 'fr' || stored === 'ar') return stored as SupportedLanguage;
  } catch {
    // ignore
  }
  return 'en';
})();

let globalDirection: Direction = (() => {
  try {
    const stored = localStorage.getItem(DIR_KEY);
    if (stored === 'ltr' || stored === 'rtl') return stored as Direction;
  } catch {
    // ignore
  }
  return globalLanguage === 'ar' ? 'rtl' : 'ltr';
})();

const listeners = new Set<() => void>();

function syncDOM() {
  if (typeof document !== 'undefined') {
    const root = document.documentElement;
    if (globalTheme === 'dark') {
      root.classList.add('dark');
    } else {
      root.classList.remove('dark');
    }
    root.setAttribute('dir', globalDirection);
    root.setAttribute('lang', globalLanguage);
    root.setAttribute('data-design-theme', globalDesignTheme);
    root.classList.remove('theme-corporate', 'theme-microsoft', 'theme-apple', 'theme-sap');
    root.classList.add(`theme-${globalDesignTheme}`);

    if (document.body) {
      document.body.setAttribute('data-design-theme', globalDesignTheme);
      document.body.classList.remove('theme-corporate', 'theme-microsoft', 'theme-apple', 'theme-sap');
      document.body.classList.add(`theme-${globalDesignTheme}`);
    }
  }
}

// Initial DOM sync on load
syncDOM();

export function useTheme() {
  const [, setTick] = useState(0);

  useEffect(() => {
    syncDOM();
    const onChange = () => setTick(t => t + 1);
    listeners.add(onChange);
    return () => {
      listeners.delete(onChange);
    };
  }, []);

  const toggleTheme = useCallback(() => {
    globalTheme = globalTheme === 'light' ? 'dark' : 'light';
    try {
      localStorage.setItem(THEME_KEY, globalTheme);
    } catch {
      // ignore
    }
    syncDOM();
    listeners.forEach(fn => fn());
  }, []);

  const setTheme = useCallback((newTheme: Theme) => {
    globalTheme = newTheme;
    try {
      localStorage.setItem(THEME_KEY, globalTheme);
    } catch {
      // ignore
    }
    syncDOM();
    listeners.forEach(fn => fn());
  }, []);

  const setDesignTheme = useCallback((theme: DesignTheme) => {
    globalDesignTheme = theme;
    try {
      localStorage.setItem(DESIGN_THEME_KEY, theme);
    } catch {
      // ignore
    }
    syncDOM();
    listeners.forEach(fn => fn());
  }, []);

  const setLanguage = useCallback((lang: SupportedLanguage) => {
    globalLanguage = lang;
    globalDirection = lang === 'ar' ? 'rtl' : 'ltr';
    try {
      localStorage.setItem(LANG_KEY, lang);
      localStorage.setItem(DIR_KEY, globalDirection);
    } catch {
      // ignore
    }
    syncDOM();
    listeners.forEach(fn => fn());
  }, []);

  const toggleDirection = useCallback(() => {
    globalDirection = globalDirection === 'ltr' ? 'rtl' : 'ltr';
    try {
      localStorage.setItem(DIR_KEY, globalDirection);
    } catch {
      // ignore
    }
    syncDOM();
    listeners.forEach(fn => fn());
  }, []);

  return {
    theme: globalTheme,
    toggleTheme,
    setTheme,
    designTheme: globalDesignTheme,
    setDesignTheme,
    designThemes: DESIGN_THEMES,
    language: globalLanguage,
    setLanguage,
    direction: globalDirection,
    toggleDirection,
    t: translations[globalLanguage]
  };
}

export function resetThemeDefaults() {
  globalTheme = 'light';
  globalDesignTheme = 'corporate';
  globalLanguage = 'en';
  globalDirection = 'ltr';
  try {
    localStorage.removeItem(THEME_KEY);
    localStorage.removeItem(DESIGN_THEME_KEY);
    localStorage.removeItem(LANG_KEY);
    localStorage.removeItem(DIR_KEY);
  } catch {
    // ignore
  }
  syncDOM();
  listeners.forEach(fn => fn());
}

