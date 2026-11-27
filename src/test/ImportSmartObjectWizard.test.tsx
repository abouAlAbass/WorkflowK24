import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { ImportSmartObjectWizardModal } from '../components/modals/ImportSmartObjectWizardModal';
import { repositoryManager } from '../services/repositoryManager';
import type { ConnectionRecord, DiscoveredEntity } from '../types/workflow';

describe('ImportSmartObjectWizardModal (Étape E.2c)', () => {
  const mockConnections: ConnectionRecord[] = [
    {
      id: 'conn-pg-hr',
      name: 'HR PostgreSQL',
      baseUrl: 'postgresql://hr.internal:5432/hr',
      authType: 'basic',
      hasSecretRef: false,
      allowedHosts: [],
      defaultHeaders: {},
      version: 1,
      createdBy: 'adel.sebti@corp.internal',
      createdAt: '2026-01-01T00:00:00Z',
      updatedAt: '2026-01-01T00:00:00Z',
      serviceType: 'postgres',
      allowWrite: true
    }
  ];

  const mockEntities: DiscoveredEntity[] = [
    {
      entityName: 'audit_summary_view',
      displayName: 'Audit Summary View',
      detectedPrimaryKey: null,
      schemaName: 'public',
      entityType: 'View',
      fields: [
        {
          name: 'employee_name',
          sourceType: 'varchar',
          mappedSmartObjectType: 'string',
          isNullable: false,
          isPrimaryKey: false,
          suggestedMasked: false
        },
        {
          name: 'salary',
          sourceType: 'numeric',
          mappedSmartObjectType: 'number',
          isNullable: true,
          isPrimaryKey: false,
          suggestedMasked: true
        },
        {
          name: 'iban',
          sourceType: 'varchar',
          mappedSmartObjectType: 'string',
          isNullable: true,
          isPrimaryKey: false,
          suggestedMasked: true
        }
      ]
    }
  ];

  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('previews discovered columns, pre-checks sensitive masked columns, and displays warning when no primary key is selected', async () => {
    const repo = repositoryManager.getRepository();
    const discoverSpy = vi
      .spyOn(repo, 'discoverConnectionEntities')
      .mockResolvedValue(mockEntities);

    const onClose = vi.fn();
    const onGenerated = vi.fn();

    render(
      <ImportSmartObjectWizardModal
        isOpen={true}
        onClose={onClose}
        onGenerated={onGenerated}
        initialConnections={mockConnections}
      />
    );

    // Step 1: Discover entities on selected connection
    fireEvent.click(screen.getByTestId('discover-entities-btn'));

    await waitFor(() => {
      expect(discoverSpy).toHaveBeenCalledWith('conn-pg-hr');
      expect(screen.getByTestId('discovered-entity-audit_summary_view')).toBeInTheDocument();
    });

    // Proceed to Step 2 (Column Preview)
    fireEvent.click(screen.getByTestId('import-next-step-btn'));

    // Verify preview table displays all discovered columns
    expect(screen.getByTestId('import-columns-preview-table')).toBeInTheDocument();
    expect(screen.getByTestId('col-include-employee_name')).toBeChecked();
    expect(screen.getByTestId('col-include-salary')).toBeChecked();
    expect(screen.getByTestId('col-include-iban')).toBeChecked();

    // Verify sensitive columns (salary, iban) have masked pre-checked, while normal column is unchecked
    expect(screen.getByTestId('col-mask-salary')).toBeChecked();
    expect(screen.getByTestId('col-mask-iban')).toBeChecked();
    expect(screen.getByTestId('col-mask-employee_name')).not.toBeChecked();

    // Verify visual warning is displayed when no primary key is selected ("Update/Delete désactivés")
    const noPkWarning = screen.getByTestId('no-pk-warning');
    expect(noPkWarning).toBeInTheDocument();
    expect(noPkWarning).toHaveTextContent(/Update\/Delete désactivés/i);

    // Selecting a primary key removes the warning
    fireEvent.change(screen.getByTestId('import-so-pk-select'), {
      target: { value: 'employee_name' }
    });
    expect(screen.queryByTestId('no-pk-warning')).not.toBeInTheDocument();
  });
});
