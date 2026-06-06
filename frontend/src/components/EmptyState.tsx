import type { ReactNode } from 'react';

// Generic empty-state shell used across Candidates / Screening / Generator pages.
// Visual style intentionally matches the empty Positions state for consistency.
interface Props {
  icon?: ReactNode;
  title: string;
  description?: string;
  action?: ReactNode;
  footer?: ReactNode;
}

export function EmptyState({ icon, title, description, action, footer }: Props) {
  return (
    <div className="card flex flex-col items-center text-center py-12 gap-3 border-dashed">
      {icon && (
        <div className="w-12 h-12 rounded-2xl bg-blue-600/15 border border-blue-500/30 flex items-center justify-center text-blue-400">
          {icon}
        </div>
      )}
      <div className="max-w-md">
        <h2 className="text-base font-semibold text-gray-100">{title}</h2>
        {description && <p className="text-sm text-gray-500 mt-1">{description}</p>}
      </div>
      {action}
      {footer && <p className="text-xs text-gray-600 mt-1">{footer}</p>}
    </div>
  );
}
