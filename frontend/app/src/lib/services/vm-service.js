import { env } from '$env/dynamic/public';
import ky from 'ky';

const clientApi = ky.create({
  prefixUrl: env.PUBLIC_CLIENT_API,
  timeout: 10000,
  hooks: {
    beforeError: [
      async (error) => {
        const { response } = error;
        if (response?.body) {
          try {
            const { message } = await response.json();
            if (message) {
              error.message = message;
            }
          } catch {
            error.message = 'Something went wrong. Please try again later.';
          }
        }
        return error;
      }
    ]
  }
});

const backendApi = ky.create({
  prefixUrl: env.PUBLIC_BACKEND_API,
  timeout: 10000,
  hooks: {
    beforeError: [
      async (error) => {
        const { response } = error;
        if (response?.body) {
          try {
            const { message } = await response.json();
            if (message) {
              error.message = message;
            }
          } catch {
            error.message = 'Something went wrong. Please try again later.';
          }
        }
        return error;
      }
    ]
  }
});

export const vmService = {
  async createVMBooking(bookingDetails) {
    return await clientApi.post('vm-booking/create', { json: bookingDetails }).text();
  },

  async getVMBookingsBackend(cookie) {
    return await backendApi
      .get('vm-booking/all', {
        headers: {
          Cookie: `token=${cookie}`
        }
      })
      .json();
  },

  async getVMBookings() {
    return await clientApi.get('vm-booking/all').json();
  },

  async getVMBookingsByUserBackend(cookie, id) {
    return await backendApi
      .get(`vm-booking/owner/${id}`, {
        headers: {
          Cookie: `token=${cookie}`
        }
      })
      .json();
  },

  async getVMBookingsByUser(id) {
    return await backendApi.get(`vm-booking/owner/${id}`).json();
  },

  async getVMBookingById(id) {
    return await clientApi.get(`vm-booking/${id}`).json();
  },

  async getVMBookingByIdBackend(cookie, id) {
    return await backendApi
      .get(`vm-booking/${id}`, {
        headers: {
          Cookie: `token=${cookie}`
        }
      })
      .json();
  },

  async getVMsAvailableCount() {
    return await clientApi.get('vcenter/all/available').json();
  },

  async getVMsAvailableCountBackend(cookie) {
    return await backendApi
      .get('vcenter/all/available', {
        headers: {
          Cookie: `token=${cookie}`
        }
      })
      .json();
  },

  async deleteVMBooking(id) {
    return await clientApi.delete(`vm-booking/delete/${id}`).text();
  },

  async getVMTemplates() {
    return await clientApi.get('script/vm/templates').json();
  },

  async resetVMTemplates() {
    await clientApi.delete('script/vm/reset-templates');
  },

  async getVMTemplatesBackend(cookie) {
    return await backendApi
      .get('script/vm/templates', {
        headers: {
          Cookie: `token=${cookie}`
        }
      })
      .json();
  },

  async acceptVMBooking(id) {
    return await clientApi.put(`vm-booking/accept/${id}`).json();
  },

  async extendVmBooking(extendDetails) {
    return await clientApi.post('extention-request/create', { json: extendDetails }).json();
  },

  async acceptExtendVmBooking(id) {
    return await clientApi.put(`extention-request/accept-extention/${id}`).json();
  },

  async deleteExtendVmBooking(id) {
    return await clientApi.delete(`extention-request/delete/${id}`).json();
  },

  async getVmInfo(uuid) {
    return await clientApi
      .get(`script/vm/get-ip/${uuid}`, {
        retry: {
          limit: 3
        },
        timeout: 30000
      })
      .json();
  },

  async resetVmPower(name) {
    await clientApi.get(`script/vm/reset-power/${name}`).json();
  },

  async getClusterInfo() {
    return await clientApi
      .get('script/cluster-info', {
        retry: {
          limit: 3
        },
        timeout: 60000
      })
      .json();
  },

  async getClusterInfoBackend(cookie) {
    return await backendApi
      .get('script/cluster-info', {
        headers: {
          Cookie: `token=${cookie}`
        }
      })
      .json();
  },

  async updateResources(resources) {
    return await clientApi.put('script/vm/update-resources', { json: resources }).json();
  },

  async attachStorage(storageOption) {
    const jsonData = {
      vmName: storageOption.vmName,
      amountGb: Number(storageOption.selectedStorage)
    };
    return await clientApi.post('script/vm/attach-storage', { json: jsonData }).json();
  },

  async getIsoListBackend(cookie) {
    return await backendApi
      .get('script/vm/iso-list', {
        headers: {
          Cookie: `token=${cookie}`
        }
      })
      .json();
  },

  async attachIso(info) {
    return await clientApi.post('script/vm/attach-iso', { json: info }).json();
  },

  async detachIso(vmName) {
    return await clientApi.post('script/vm/detach-iso', { json: { vmName } }).json();
  },

  async getUsageInfoBackend(cookie, vmName) {
    return await backendApi
      .get(`script/vm/usage-info/${vmName}`, {
        headers: {
          Cookie: `token=${cookie}`
        }
      })
      .json();
  },

  async updateResources(resources) {
    return await clientApi.put('script/vm/update-resources', { json: resources }).json();
  },

  async getIsoListBackend(cookie) {
    return await backendApi
      .get('script/vm/iso-list', {
        headers: {
          Cookie: `token=${cookie}`
        }
      })
      .json();
  },

  async attachIso(info) {
    return await clientApi.post('script/vm/attach-iso', { json: info }).json();
  },

  async detachIso(vmName) {
    return await clientApi.post('script/vm/detach-iso', { json: { vmName } }).json();
  }
};
