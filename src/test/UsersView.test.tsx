import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { UsersView } from '../components/views/UsersView';
import { useWorkflowStore } from '../hooks/useWorkflowStore';

describe('UsersView User Administration', () => {
  beforeEach(() => {
    // Reset store users to a known state
    useWorkflowStore.setState({
      users: [
        {
          id: 'usr-1',
          name: 'Adel Sebti',
          email: 'adel.sebti@corp.internal',
          role: 'BPM Administrator',
          group: 'Engineering',
          status: 'Active',
          avatarUrl: '/avatars/user-2.svg'
        },
        {
          id: 'usr-2',
          name: 'Karim Khmissi',
          email: 'karim.khmissi@corp.internal',
          role: 'Process Manager',
          group: 'Finance',
          status: 'Active',
          avatarUrl: '/avatars/user-3.svg'
        }
      ]
    });
  });

  it('renders registered users in table with roles and status', () => {
    render(<UsersView />);

    expect(screen.getByText('Adel Sebti')).toBeInTheDocument();
    expect(screen.getByText('adel.sebti@corp.internal')).toBeInTheDocument();
    expect(screen.getAllByText('BPM Administrator').length).toBeGreaterThanOrEqual(1);
    expect(screen.getByText('Karim Khmissi')).toBeInTheDocument();
    expect(screen.getAllByText('Process Manager').length).toBeGreaterThanOrEqual(1);
  });

  it('filters users dynamically based on search query', () => {
    render(<UsersView />);

    const searchInput = screen.getByPlaceholderText('Search by name, email, or department...');
    fireEvent.change(searchInput, { target: { value: 'Karim' } });

    expect(screen.getByText('Karim Khmissi')).toBeInTheDocument();
    expect(screen.queryByText('Adel Sebti')).not.toBeInTheDocument();
  });

  it('opens Add User modal when clicking Add User or Group button', () => {
    render(<UsersView />);

    const addButton = screen.getByTestId('add-user-button');
    fireEvent.click(addButton);

    expect(screen.getByText('Add New BPM User')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('e.g. Walid Mansouri')).toBeInTheDocument();
    expect(screen.getByPlaceholderText('w.mansouri@enterprise.internal')).toBeInTheDocument();
  });

  it('creates new user and appends to table upon submitting form', async () => {
    render(<UsersView />);

    fireEvent.click(screen.getByTestId('add-user-button'));

    const nameInput = screen.getByPlaceholderText('e.g. Walid Mansouri');
    const emailInput = screen.getByPlaceholderText('w.mansouri@enterprise.internal');

    fireEvent.change(nameInput, { target: { value: 'Walid Mansouri' } });
    fireEvent.change(emailInput, { target: { value: 'walid.mansouri@corp.internal' } });

    const submitBtn = screen.getByText('Create User');
    fireEvent.click(submitBtn);

    await waitFor(() => {
      expect(screen.getByText('Walid Mansouri')).toBeInTheDocument();
      expect(screen.getByText('walid.mansouri@corp.internal')).toBeInTheDocument();
    });
  });

  it('prompts and deletes user when clicking delete button', async () => {
    vi.spyOn(window, 'confirm').mockReturnValue(true);

    render(<UsersView />);

    expect(screen.getByText('Adel Sebti')).toBeInTheDocument();

    const deleteButtons = screen.getAllByTitle('Delete user');
    fireEvent.click(deleteButtons[0]);

    await waitFor(() => {
      expect(screen.queryByText('Adel Sebti')).not.toBeInTheDocument();
    });
  });
});
