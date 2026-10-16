import React from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter, Route, Routes } from 'react-router-dom';
import './styles.css';
import { Layout } from './components/Layout';
import { HomePage } from './pages/HomePage';
import { CatalogPage } from './pages/CatalogPage';
import { MoviePage } from './pages/MoviePage';
import { SeriesPage } from './pages/SeriesPage';
import { AdminModal } from './pages/AdminModal';

function App() {
  const [adminOpen, setAdminOpen] = React.useState(false);
  return (
    <BrowserRouter>
      <Layout onAdmin={() => setAdminOpen(true)}>
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/catalog" element={<CatalogPage />} />
          <Route path="/movie/:id" element={<MoviePage />} />
          <Route path="/series/:id" element={<SeriesPage />} />
          <Route path="*" element={<div className="catalog-section"><h2>404</h2></div>} />
        </Routes>
      </Layout>
      {adminOpen && <AdminModal onClose={() => setAdminOpen(false)} />}
    </BrowserRouter>
  );
}

createRoot(document.getElementById('root')!).render(<App />);