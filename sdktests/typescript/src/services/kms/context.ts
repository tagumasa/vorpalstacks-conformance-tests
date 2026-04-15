export interface KmsState {
  keyID: string;
  rsaKeyID: string;
  hmacKeyID: string;
  ciphertextBlob?: Uint8Array;
  signature?: Uint8Array;
  macValue?: Uint8Array;
}
