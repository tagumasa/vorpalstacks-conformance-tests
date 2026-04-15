export const SCOPE = 'CLOUDFRONT' as const;

export interface WebACLState {
  name: string;
  id: string;
  arn: string;
  lockToken: string;
}
