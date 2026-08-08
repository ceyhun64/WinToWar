// Backend Auth DTO'larıyla (api/Models/Auth/Dtos/AuthDtos.cs) birebir eşleşen tipler.

export interface PlayerAccountDto {
  id: string;
  email: string;
  displayName: string;
  role: "Player" | "Admin";
  status: "Active" | "Suspended" | "PendingDeletion" | "Deleted";
  emailVerified: boolean;
  hasPassword: boolean;
  googleLinked: boolean;
}

export interface AuthResponseDto {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  player: PlayerAccountDto;
}

export interface AuthErrorResponse {
  code: string;
  message: string;
}
