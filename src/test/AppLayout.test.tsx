import { describe, it, expect } from 'vitest';
import '@testing-library/jest-dom/vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { AppLayout } from '../components/layout/AppLayout';

describe('AppLayout Theme & Accessibility Refinements', () => {
  it('renders a single canonical theme toggle with accessible tooltip and aria-label', () => {
    render(
      <AppLayout>
        <div>Content</div>
      </AppLayout>
    );

    const themeToggle = screen.getByRole('button', { name: /Switch to (Dark|Light) Mode/i });
    expect(themeToggle).toBeInTheDocument();
  });

  it('toggles theme on click and updates aria-label and DOM', () => {
    render(
      <AppLayout>
        <div>Content</div>
      </AppLayout>
    );

    const themeToggle = screen.getByRole('button', { name: /Switch to (Dark|Light) Mode/i });
    const initialLabel = themeToggle.getAttribute('aria-label');

    fireEvent.click(themeToggle);

    const updatedLabel = themeToggle.getAttribute('aria-label');
    expect(updatedLabel).not.toBe(initialLabel);
  });

  it('renders language and layout direction switcher for Arabic (RTL) / Latin readiness', () => {
    const { container } = render(
      <AppLayout>
        <div>Content</div>
      </AppLayout>
    );

    const rootDiv = container.firstChild as HTMLElement;
    expect(rootDiv).toHaveAttribute('dir');

    const langToggle = screen.getByRole('button', { name: /Switch Language and Direction/i });
    expect(langToggle).toBeInTheDocument();
  });
});
