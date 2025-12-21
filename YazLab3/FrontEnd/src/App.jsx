import { Routes, Route } from "react-router-dom";
import Login from "./Login-Register/Login";
import Register from "./Login-Register/Register";
import ForgotPassword from "./Login-Register/forgot_password";
import ForgotPasswordReset from "./Login-Register/forgot_password_reset";
import Menu from "./Menu/menu";

function App() {
  return (
    <Routes>
      <Route path="/" element={<Login />} />
      <Route path="/register" element={<Register />} />
      <Route path="/forgot_password" element={<ForgotPassword />} />
      <Route path="/reset-password" element={<ForgotPasswordReset />}/>
      <Route path="/menu" element={<Menu />} />
    </Routes>
  );
}

export default App;