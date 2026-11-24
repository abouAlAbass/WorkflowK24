import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { NodePalette } from '../components/palette/NodePalette';

describe('NodePalette Compact Layout & Readability', () => {
  it('renders all 4 workflow sample templates without missing any', () => {
    render(<NodePalette />);

    expect(screen.getByRole('button', { name: /Onboarding/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Expense Appr\./i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /IT System/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Blank Canvas/i })).toBeInTheDocument();
  });

  it('renders all 5 component items with single-line descriptions and tooltip titles', () => {
    render(<NodePalette />);

    expect(screen.getByText('Start / Trigger')).toBeInTheDocument();
    expect(screen.getByText('Approval Task')).toBeInTheDocument();
    expect(screen.getByText('Send Email')).toBeInTheDocument();
    expect(screen.getByText('Decision / Rule')).toBeInTheDocument();
    expect(screen.getByText('End Event')).toBeInTheDocument();

    const descriptions = [
      'Defines event starting the workflow',
      'User or Group decision task with dynamic outcome handles',
      'Send automated email notification',
      'Conditional logical branching (IF / THEN)',
      'Terminal outcome node for process execution',
    ];

    descriptions.forEach((desc) => {
      const el = screen.getByText(desc);
      expect(el).toBeInTheDocument();
      expect(el).toHaveAttribute('title', desc);
      expect(el.className).toContain('truncate');
    });
  });
});
