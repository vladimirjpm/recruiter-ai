import { useRef, useState } from 'react';

interface Props {
  onFiles: (files: File[]) => void;
  isUploading: boolean;
}

export function DropZone({ onFiles, isUploading }: Props) {
  const [isDragging, setIsDragging] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  const handleDrop = (e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
    const files = Array.from(e.dataTransfer.files).filter(f => f.type === 'application/pdf');
    if (files.length > 0) onFiles(files);
  };

  const handleChange = (e: React.ChangeEvent<HTMLInputElement>) => {
    const files = Array.from(e.target.files ?? []);
    if (files.length > 0) onFiles(files);
    e.target.value = '';
  };

  return (
    <div
      onDragEnter={() => setIsDragging(true)}
      onDragOver={e => { e.preventDefault(); setIsDragging(true); }}
      onDragLeave={() => setIsDragging(false)}
      onDrop={handleDrop}
      onClick={() => !isUploading && inputRef.current?.click()}
      className={[
        'border-2 border-dashed rounded-lg px-6 py-8 text-center transition-colors',
        isDragging
          ? 'border-blue-500 bg-blue-900/20'
          : 'border-gray-600 hover:border-gray-400 bg-gray-800/50',
        isUploading ? 'opacity-50 cursor-not-allowed' : 'cursor-pointer',
      ].join(' ')}
    >
      <input
        ref={inputRef}
        type="file"
        accept=".pdf,application/pdf"
        multiple
        className="hidden"
        onChange={handleChange}
        disabled={isUploading}
      />
      {isUploading ? (
        <p className="text-sm text-gray-400">Uploading...</p>
      ) : (
        <>
          <p className="text-sm text-gray-300">
            Drag &amp; drop PDF files here, or{' '}
            <span className="text-blue-400 underline">browse</span>
          </p>
          <p className="text-xs text-gray-500 mt-1">Up to 10 files · 5 MB each</p>
        </>
      )}
    </div>
  );
}
