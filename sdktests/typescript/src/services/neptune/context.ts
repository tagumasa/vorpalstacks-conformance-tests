export interface NeptuneState {
  region: string;
  subnetGroupName: string;
  clusterParamGroupName: string;
  clusterName: string;
  snapshotName: string;
  instanceName: string;
  globalClusterId: string;
  endpointName: string;
  clusterArn: string;
  instanceArn: string;
  subnetCreated: boolean;
  cpgCreated: boolean;
  clusterCreated: boolean;
  instanceCreated: boolean;
  snapshotCreated: boolean;
  subName: string;
}
