import { describe, it, expect, vi } from 'vitest';
import '@testing-library/jest-dom/vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { HeaderToolbar } from '../components/header/HeaderToolbar';

describe('HeaderToolbar UI & UX Refinements', () => {
  const defaultProps = {
    onOpenJsonModal: vi.fn(),
    onOpenValidationModal: vi.fn(),
  };

  it('renders primary visible buttons: Test Simulation, Validate, and Save Definition', () => {
    render(<HeaderToolbar {...defaultProps} />);

    expect(screen.getByRole('button', { name: /Test Simulation/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Validate/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Save Definition/i })).toBeInTheDocument();
  });

  it('renders Demo Mode as a status badge/chip indicator', () => {
    render(<HeaderToolbar {...defaultProps} />);

    const modeIndicator = screen.getByLabelText(/Current Mode:/i);
    expect(modeIndicator).toBeInTheDocument();
    expect(modeIndicator).toHaveTextContent(/Demo Mode/i);
  });

  it('renders save status indicator for auto-saved draft vs named version', () => {
    render(<HeaderToolbar {...defaultProps} />);

    expect(screen.getByText(/All changes saved|Unsaved changes/i)).toBeInTheDocument();
  });

  it('groups Export, Import, Saved DB, and Reset into the accessible More (⋯) overflow menu', () => {
    render(<HeaderToolbar {...defaultProps} />);

    // Before opening overflow menu, overflow menu items are not in document
    const moreButton = screen.getByRole('button', { name: /More actions/i });
    expect(moreButton).toBeInTheDocument();
    expect(screen.queryByRole('menuitem', { name: /Export JSON/i })).not.toBeInTheDocument();

    // Click to open overflow menu
    fireEvent.click(moreButton);

    // Overflow menu items are now accessible
    expect(screen.getByRole('menuitem', { name: /Export JSON/i })).toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: /Import JSON/i })).toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: /Saved DB/i })).toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: /Reset Canvas/i })).toBeInTheDocument();
  });

  it('closes overflow menu on Escape key press for keyboard accessibility', () => {
    render(<HeaderToolbar {...defaultProps} />);

    const moreButton = screen.getByRole('button', { name: /More actions/i });
    fireEvent.click(moreButton);
    expect(screen.getByRole('menuitem', { name: /Export JSON/i })).toBeInTheDocument();

    // Press Escape
    fireEvent.keyDown(moreButton, { key: 'Escape', code: 'Escape' });
    expect(screen.queryByRole('menuitem', { name: /Export JSON/i })).not.toBeInTheDocument();
  });

  it('renders workflow title with hover tooltip and does not have duplicate dark mode toggle in toolbar', () => {
    render(<HeaderToolbar {...defaultProps} />);

    const titleInput = screen.getByLabelText('Workflow Title');
    expect(titleInput).toBeInTheDocument();
    expect(titleInput).toHaveAttribute('title');

    // Duplicate theme toggle button should NOT be in the toolbar
    expect(screen.queryByText(/Light Mode/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/Dark Mode/i)).not.toBeInTheDocument();
  });
});
