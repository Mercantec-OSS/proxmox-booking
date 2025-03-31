import { vmService } from '$lib/services/vm-service';

export async function load({ cookies }) {
  let vmData = [];
  let clusterInfo = null;
  const token = cookies.get('token');

  try {
    [vmData, clusterInfo] = await Promise.all([
      vmService.getVMBookingsBackend(token).catch((error) => {
        console.error(error);
        return [];
      }),
      vmService.getClusterInfoBackend(token).catch((error) => {
        console.error(error);
        return null;
      })
    ]);
  } catch (error) {
    console.error(error);
  }

  return {
    vmData,
    clusterInfo
  };
}
