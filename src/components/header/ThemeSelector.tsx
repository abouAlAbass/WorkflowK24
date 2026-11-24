import React, { useState, useRef, useEffect } from 'react';
import { Palette, Check, Sun, Moon, Sparkles, ChevronDown } from 'lucide-react';
import { useTheme } from '../../hooks/useTheme';
import type { DesignTheme } from '../../hooks/useTheme';

export const ThemeSelector: React.FC = () => {
  const { theme, toggleTheme, designTheme, setDesignTheme, designThemes } = useTheme();
  const [isOpen, setIsOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  const activeTheme = designThemes.find(t => t.id === designTheme) || designThemes[0];

  useEffect(() => {
    const handleClickOutside = (e: MouseEvent) => {
      if (dropdownRef.current && !dropdownRef.current.contains(e.target as Node)) {
        setIsOpen(false);
      }
    };
    if (isOpen) {
      document.addEventListener('mousedown', handleClickOutside);
    }
    return () => {
      document.removeEventListener('mousedown', handleClickOutside);
    };
  }, [isOpen]);

  return (
    <div className="relative" ref={dropdownRef}>
      {/* Trigger Button */}
      <button
        type="button"
        onClick={() => setIsOpen(!isOpen)}
        data-testid="design-theme-trigger"
        aria-label={`Design Theme: ${activeTheme.name}`}
        aria-expanded={isOpen}
        className="px-2.5 py-1.5 rounded-xl border border-slate-200 dark:border-slate-800 bg-white dark:bg-slate-900 hover:bg-slate-50 dark:hover:bg-slate-800 text-xs font-bold text-[#0F172A] dark:text-slate-100 flex items-center gap-2 shadow-2xs transition-all cursor-pointer focus:outline-none focus:ring-2 focus:ring-blue-500"
        title="Switch Design System (Corporate, Microsoft, Apple, SAP)"
      >
        <Palette className="w-3.5 h-3.5 text-blue-600 dark:text-blue-400 shrink-0" />
        <span className="flex items-center gap-1.5">
          <span 
            className="w-2.5 h-2.5 rounded-full inline-block shadow-2xs" 
            style={{ backgroundColor: theme === 'dark' ? activeTheme.darkPrimary : activeTheme.lightPrimary }}
          />
          <span className="hidden md:inline">{activeTheme.name.split(' ')[0]}</span>
        </span>
        <ChevronDown className={`w-3 h-3 text-slate-400 transition-transform duration-200 ${isOpen ? 'rotate-180' : ''}`} />
      </button>

      {/* Popover Dropdown */}
      {isOpen && (
        <div 
          data-testid="design-theme-dropdown"
          className="absolute end-0 top-full mt-2 w-80 bg-white dark:bg-[#1E293B] border border-slate-200 dark:border-slate-700 rounded-2xl shadow-2xl p-3 z-50 animate-in fade-in zoom-in-95 duration-150 space-y-3"
        >
          {/* Header */}
          <div className="flex items-center justify-between pb-2 border-b border-slate-100 dark:border-slate-700">
            <div>
              <h4 className="text-xs font-extrabold text-[#0F172A] dark:text-white flex items-center gap-1.5">
                <Sparkles className="w-3.5 h-3.5 text-blue-600 dark:text-blue-400" />
                <span>Design System & Presets</span>
              </h4>
              <p className="text-[10px] text-slate-500 font-medium">Dynamic fonts, geometry & palettes</p>
            </div>
            
            <button
              onClick={toggleTheme}
              aria-label="Toggle Dark/Light Mode"
              className="p-1.5 rounded-lg border border-slate-200 dark:border-slate-700 hover:bg-slate-100 dark:hover:bg-slate-800 text-slate-700 dark:text-slate-200 cursor-pointer transition-colors"
              title={`Switch to ${theme === 'dark' ? 'Light' : 'Dark'} mode`}
            >
              {theme === 'dark' ? (
                <Sun className="w-3.5 h-3.5 text-amber-400" />
              ) : (
                <Moon className="w-3.5 h-3.5 text-slate-700" />
              )}
            </button>
          </div>

          {/* Theme Presets List */}
          <div className="space-y-1.5">
            {designThemes.map((option) => {
              const isSelected = option.id === designTheme;
              const primaryColor = theme === 'dark' ? option.darkPrimary : option.lightPrimary;

              return (
                <button
                  key={option.id}
                  type="button"
                  data-testid={`theme-option-${option.id}`}
                  onClick={() => {
                    setDesignTheme(option.id as DesignTheme);
                    setIsOpen(false);
                  }}
                  className={`w-full p-2.5 rounded-xl border text-left transition-all cursor-pointer flex items-start gap-2.5 ${
                    isSelected
                      ? 'border-blue-500 dark:border-blue-500 bg-blue-50/60 dark:bg-blue-950/40 shadow-xs'
                      : 'border-slate-200/80 dark:border-slate-800 bg-white dark:bg-slate-900 hover:bg-slate-50 dark:hover:bg-slate-800/80'
                  }`}
                >
                  {/* Swatch Indicator */}
                  <div 
                    className="w-4 h-4 rounded-full mt-0.5 shrink-0 border border-white dark:border-slate-800 shadow-xs flex items-center justify-center text-white"
                    style={{ backgroundColor: primaryColor }}
                  >
                    {isSelected && <Check className="w-2.5 h-2.5 stroke-[3]" />}
                  </div>

                  <div className="min-w-0 flex-1">
                    <div className="flex items-center justify-between gap-1">
                      <h5 className="text-xs font-bold text-[#0F172A] dark:text-white truncate">
                        {option.name}
                      </h5>
                      <span className="text-[9px] font-mono font-bold px-1.5 py-0.2 rounded bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400">
                        {option.fontLabel}
                      </span>
                    </div>

                    <p className="text-[10px] text-slate-500 dark:text-slate-400 mt-0.5 line-clamp-1 leading-tight font-medium">
                      {option.description}
                    </p>
                  </div>
                </button>
              );
            })}
          </div>

          {/* Active Mode Info */}
          <div className="pt-2 border-t border-slate-100 dark:border-slate-700/60 flex items-center justify-between text-[10px] text-slate-500 font-medium px-1">
            <span>Mode: <strong className="capitalize text-slate-700 dark:text-slate-300">{theme} Mode</strong></span>
            <span>Font: <strong className="text-slate-700 dark:text-slate-300">{activeTheme.fontLabel}</strong></span>
          </div>
        </div>
      )}
    </div>
  );
};
