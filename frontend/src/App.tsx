import { Route, Routes } from 'react-router'
import LandingPage from './pages/LandingPage'
import BookingPage from './pages/BookingPage'
import OwnerPage from './pages/OwnerPage'

function App() {
  return (
    <Routes>
      <Route path="/" element={<LandingPage />} />
      <Route path="/booking" element={<BookingPage />} />
      <Route path="/booking/:eventTypeId" element={<BookingPage />} />
      <Route path="/owner" element={<OwnerPage />} />
    </Routes>
  )
}

export default App
