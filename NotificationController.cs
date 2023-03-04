using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Suggestions.WebAPI.Base;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Check.WebAPI.CMS
{
    [CheckAnonymous]
    public class NotificationController : EntityController<CheckDbContext, Notification, long>, IEntityController<CheckDbContext, Notification, long>
    {
        private readonly NotificationService _notificationService;
        private readonly ILogger logger;
        public IEntityService<CheckDbContext, User, string> Service { get; set; }
        public NotificationController(UserManager<User> userManager, 
            NotificationService notificationService, 
            ILogger logger): base(userManager, notificationService)
        {
            this._notificationService = notificationService;
            this.logger = logger;
        }

        [HttpGet]
        [CheckAuth(PrivilegeType.Read)]
        public ServerResult<bool> Read(int id)
        {
            bool result = _notificationService.ReadNotification(id, CurrentUserId);
            return result.ToServerResult(result);
        }
        
        [HttpPost]
        [CheckAuth(PrivilegeType.Read)]
        public  ServerResult<TableData<NotificationView>> GetPaged([FromBody] QueryInfo queryInfo)
        {
            try
            {
                return _notificationService.GetPaged(queryInfo).ToServerResult();
            }
            catch (Exception ex)
            {
                return ex.ToServerResult< TableData<NotificationView>>();
            }                 
        }
        
        [HttpPost]
        [CheckAuth(PrivilegeType.Delete)]
        public  ServerResult<NotificationView> GetByID(long id)
        {
            return base.GetByID<NotificationView>(id);
        }
        
        [HttpPost]
        [CheckAuth(PrivilegeType.Delete)]
        public override ServerResult<long> DeleteByIDs([FromBody]List<long> ids)
        {
            return base.DeleteByIDs(ids);
        }
        
        [HttpGet]
        [CheckAuth(PrivilegeType.Delete)]
        public override ServerResult<long> DeleteByID(long id)
        {
            return base.DeleteByID(id);
        }
    }
}
