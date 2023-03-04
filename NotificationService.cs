using AutoMapper.QueryableExtensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Check.Services.CMS
{
    public class NotificationService : EntityService<CheckDbContext, Notification, long>, IEntityService<CheckDbContext, Notification, long>
    {
       // private readonly NotificationSender _notificationSender;
        public NotificationService(IHttpContextAccessor httpContextAccessor, UserManager<User> userManager, CheckDbContext db
            //,
          //   NotificationSender notificationSender
            ) : base(httpContextAccessor, userManager, db)
        {
         //   _notificationSender = notificationSender;

        }

        /// <summary>
        /// All user notifications
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public IQueryable<Notification> GetAllUserNotifications(Guid userId)
        {
            return Db.GetAll<Notification>(true).Where(d => d.UserId == userId)
                .OrderByDescending(d => d.CreateDate);
        }

        public bool InsertAll(string commaSeparatedUserIdList, MessageContact entity)
        {
            if (!string.IsNullOrEmpty(commaSeparatedUserIdList))
            {
                commaSeparatedUserIdList.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Distinct()
                    .ToList()
                    .ForEach((userId) =>
                    {
                        var notification = (Notification)entity;
                        notification.UserId = Guid.TryParse(userId.Trim(), out var id) ? id : Guid.Empty;
                        if (!notification.UserId.Equals(Guid.Empty))
                        {
                            Add(notification);
                        }
                    });

                SaveChanges();
                return true;
            }
            return false;
        }

        public bool ReadNotification(int id, string currentUserId)
        {
            try
            {
                var notif = GetByID(id);
                if (notif == null || notif.UserId.ToString() != currentUserId)
                {
                    return false;
                }

                if (notif.ReadDate.HasValue)
                {
                    return true;
                }

                notif.ReadDate = DateTime.Now;
                Update(notif);
                return true;
            }
            catch
            {
                return false;
            }
        }
        public List<NotificationView> GetNotifications()
        {
        
            var data= Db.GetAll<Notification>(true)
                .Where(d => d.UserId == UserId && d.ReadDate == null)
                .Select(n=>new NotificationView {
                    Category=n.Category,
                    CreateDate=n.CreateDate,
                    ID=n.ID,
                    Link=n.Link,
                    Message=n.Message,
                    Title=n.Title
                })
                .OrderByDescending(d => d.CreateDate)
                .Take(3).ToList();
            return data;
        }
        /// <summary>
        /// All unread user notifications
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public IQueryable<Notification> GetUnReadNotifications(Guid userId)
        {
            return GetAllUserNotifications(userId).Where(d => d.ReadDate == null);
        }
        public TableData<NotificationView> GetPaged(QueryInfo queryInfo)
        {
            var result = GetUnReadNotifications(UserId).ProjectTo<NotificationView>();
            return base.GetTableData(result, queryInfo);
        }
    }
}
