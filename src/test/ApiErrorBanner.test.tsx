import { describe, it, expect } from 'vitest';
import { render, screen, act, fireEvent } from '@testing-library/react';
import { ApiErrorBanner } from '../components/layout/ApiErrorBanner';
import { API_ERROR_EVENT } from '../services/apiAccessInterceptor';

describe('ApiErrorBanner', () => {
  it('displays an alert banner when a 403 access error event is dispatched and dismisses it', () => {
    render(<ApiErrorBanner />);
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();

    act(() => {
      window.dispatchEvent(new CustomEvent(API_ERROR_EVENT, {
        detail: { status: 403, message: 'Access denied: your role does not allow this action.' }
      }));
    });

    expect(screen.getByRole('alert')).toHaveTextContent('Access denied');

    fireEvent.click(screen.getByRole('button', { name: 'Dismiss' }));
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });
});
