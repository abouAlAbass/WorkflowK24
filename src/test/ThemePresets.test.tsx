import { describe, it, expect, beforeEach } from 'vitest';
import '@testing-library/jest-dom/vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { AppLayout } from '../components/layout/AppLayout';
import { ThemeSelector } from '../components/header/ThemeSelector';
import { DESIGN_THEMES, resetThemeDefaults } from '../hooks/useTheme';

describe('Design Theme Presets (Corporate, Microsoft, Apple, SAP)', () => {
  beforeEach(() => {
    resetThemeDefaults();
  });

  it('renders ThemeSelector trigger button displaying the active design theme', () => {
    render(<ThemeSelector />);

    const trigger = screen.getByTestId('design-theme-trigger');
    expect(trigger).toBeInTheDocument();
    expect(trigger).toHaveAttribute('aria-label', expect.stringContaining('Corporate'));
  });

  it('opens dropdown and displays all 4 design theme presets (Corporate, Microsoft, Apple, SAP)', () => {
    render(<ThemeSelector />);

    const trigger = screen.getByTestId('design-theme-trigger');
    fireEvent.click(trigger);

    expect(screen.getByTestId('design-theme-dropdown')).toBeInTheDocument();

    for (const theme of DESIGN_THEMES) {
      const option = screen.getByTestId(`theme-option-${theme.id}`);
      expect(option).toBeInTheDocument();
      expect(option).toHaveTextContent(theme.name);
      expect(option).toHaveTextContent(theme.fontLabel);
    }
  });

  it('switches to Microsoft theme with Segoe UI Variable and updates document attributes', () => {
    render(<ThemeSelector />);

    const trigger = screen.getByTestId('design-theme-trigger');
    fireEvent.click(trigger);

    const microsoftOption = screen.getByTestId('theme-option-microsoft');
    fireEvent.click(microsoftOption);

    expect(document.documentElement.getAttribute('data-design-theme')).toBe('microsoft');
    expect(document.documentElement.classList.contains('theme-microsoft')).toBe(true);
    expect(localStorage.getItem('tamam_design_theme')).toBe('microsoft');
  });

  it('switches to Apple theme with SF Pro and squircle geometry', () => {
    render(<ThemeSelector />);

    const trigger = screen.getByTestId('design-theme-trigger');
    fireEvent.click(trigger);

    const appleOption = screen.getByTestId('theme-option-apple');
    fireEvent.click(appleOption);

    expect(document.documentElement.getAttribute('data-design-theme')).toBe('apple');
    expect(document.documentElement.classList.contains('theme-apple')).toBe(true);
    expect(localStorage.getItem('tamam_design_theme')).toBe('apple');
  });

  it('switches to SAP theme with 72 / Roboto and SAP Quartz styling', () => {
    render(<ThemeSelector />);

    const trigger = screen.getByTestId('design-theme-trigger');
    fireEvent.click(trigger);

    const sapOption = screen.getByTestId('theme-option-sap');
    fireEvent.click(sapOption);

    expect(document.documentElement.getAttribute('data-design-theme')).toBe('sap');
    expect(document.documentElement.classList.contains('theme-sap')).toBe(true);
    expect(localStorage.getItem('tamam_design_theme')).toBe('sap');
  });

  it('supports light and dark appearance toggle under any design theme', () => {
    render(
      <AppLayout>
        <div>Content Area</div>
      </AppLayout>
    );

    // Initial theme: corporate light
    expect(document.documentElement.getAttribute('data-design-theme')).toBe('corporate');

    // Toggle to Dark Mode
    const darkToggle = screen.getByRole('button', { name: /Switch to (Dark|Light) Mode/i });
    act(() => {
      fireEvent.click(darkToggle);
    });

    expect(document.documentElement.classList.contains('dark')).toBe(true);
    expect(document.documentElement.getAttribute('data-design-theme')).toBe('corporate');

    // Switch to SAP while in dark mode
    const themeTrigger = screen.getByTestId('design-theme-trigger');
    fireEvent.click(themeTrigger);

    const sapOption = screen.getByTestId('theme-option-sap');
    fireEvent.click(sapOption);

    expect(document.documentElement.getAttribute('data-design-theme')).toBe('sap');
    expect(document.documentElement.classList.contains('dark')).toBe(true);
    expect(document.documentElement.classList.contains('theme-sap')).toBe(true);
  });
});
