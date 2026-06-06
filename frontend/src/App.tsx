import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { Toaster } from 'react-hot-toast';
import { Layout } from './components/Layout';
import { PositionsPage } from './pages/PositionsPage';
import { CandidatesPage } from './pages/CandidatesPage';
import { ScreeningPage } from './pages/ScreeningPage';
import { GeneratorPage } from './pages/GeneratorPage';
import { features } from './config/features';

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: 1,
    },
  },
});

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <Routes>
          <Route element={<Layout />}>
            <Route index element={<Navigate to="/positions" replace />} />
            <Route path="/positions" element={<PositionsPage />} />
            <Route path="/candidates" element={<CandidatesPage />} />
            <Route path="/screening" element={<ScreeningPage />} />
            {/* Generator is dev/QA tooling — route only exists when the feature flag is on. */}
            <Route
              path="/generator"
              element={features.syntheticCvGenerator ? <GeneratorPage /> : <Navigate to="/positions" replace />}
            />
            <Route path="*" element={<Navigate to="/positions" replace />} />
          </Route>
        </Routes>
      </BrowserRouter>
      <Toaster
        position="bottom-right"
        toastOptions={{
          style: {
            background: '#1e293b',
            color: '#f1f5f9',
            border: '1px solid #334155',
          },
        }}
      />
    </QueryClientProvider>
  );
}
