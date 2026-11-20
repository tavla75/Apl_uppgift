export interface AuthResponse {
  accessToken: string;
  email: string;
  userName: string;
  expiresAt: string;
}

export interface RegisterRequest {
  email: string;
  userName: string;
  password: string;
}

export interface LoginRequest {
  identifier: string;
  password: string;
}
