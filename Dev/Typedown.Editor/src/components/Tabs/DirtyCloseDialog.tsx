import React, { useCallback } from 'react';
import './DirtyCloseDialog.scss';

interface DirtyCloseDialogProps {
  tabName: string;
  onSave: () => void;
  onDontSave: () => void;
  onCancel: () => void;
}

const DirtyCloseDialog: React.FC<DirtyCloseDialogProps> = ({
  tabName,
  onSave,
  onDontSave,
  onCancel,
}) => {
  const handleOverlayClick = useCallback((e: React.MouseEvent) => {
    if (e.target === e.currentTarget) {
      onCancel();
    }
  }, [onCancel]);

  return (
    <div
      className="dirty-close-overlay"
      onClick={handleOverlayClick}
      onKeyDown={(e) => {
        if (e.key === 'Escape') {
          onCancel();
        }
      }}
    >
      <div
        aria-label="Unsaved changes"
        className="dirty-close-dialog"
        role="dialog"
      >
        <p className="dirty-close-dialog__message">
          Save changes to &ldquo;{tabName}&rdquo; before closing?
        </p>
        <div className="dirty-close-dialog__actions">
          <button
            className="dirty-close-dialog__button dirty-close-dialog__button--save"
            onClick={onSave}
            type="button"
          >
            Save
          </button>
          <button
            className="dirty-close-dialog__button dirty-close-dialog__button--dont-save"
            onClick={onDontSave}
            type="button"
          >
            Don&apos;t Save
          </button>
          <button
            className="dirty-close-dialog__button dirty-close-dialog__button--cancel"
            onClick={onCancel}
            type="button"
          >
            Cancel
          </button>
        </div>
      </div>
    </div>
  );
};

export default DirtyCloseDialog;
