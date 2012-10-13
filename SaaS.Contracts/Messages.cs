using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

// ReSharper disable PartialTypeWithSinglePart
// ReSharper disable UnusedMember.Local
namespace SaaS
{
    #region Generated by Lokad Code DSL
    /// <summary>
    /// Started {role}. {instance}
    /// </summary>
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class InstanceStarted : IFuncEvent
    {
        [DataMember(Order = 1)] public string CodeVersion { get; private set; }
        [DataMember(Order = 2)] public string Role { get; private set; }
        [DataMember(Order = 3)] public string Instance { get; private set; }
        
        InstanceStarted () {}
        public InstanceStarted (string codeVersion, string role, string instance)
        {
            CodeVersion = codeVersion;
            Role = role;
            Instance = instance;
        }
        
        public override string ToString()
        {
            return string.Format(@"Started {0}. {1}", Role, Instance);
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class SendMailMessage : IFuncCommand
    {
        [DataMember(Order = 1)] public Email[] To { get; private set; }
        [DataMember(Order = 2)] public string Subject { get; private set; }
        [DataMember(Order = 3)] public string Body { get; private set; }
        [DataMember(Order = 4)] public bool IsHtml { get; private set; }
        [DataMember(Order = 5)] public Email[] Cc { get; private set; }
        [DataMember(Order = 6)] public Email OptionalSender { get; private set; }
        [DataMember(Order = 7)] public Email OptionalReplyTo { get; private set; }
        
        SendMailMessage () 
        {
            To = new Email[0];
            Cc = new Email[0];
        }
        public SendMailMessage (Email[] to, string subject, string body, bool isHtml, Email[] cc, Email optionalSender, Email optionalReplyTo)
        {
            To = to;
            Subject = subject;
            Body = body;
            IsHtml = isHtml;
            Cc = cc;
            OptionalSender = optionalSender;
            OptionalReplyTo = optionalReplyTo;
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class MailMessageSent : IFuncEvent
    {
        [DataMember(Order = 1)] public Email[] To { get; private set; }
        [DataMember(Order = 2)] public string Subject { get; private set; }
        [DataMember(Order = 3)] public string Body { get; private set; }
        [DataMember(Order = 4)] public bool IsHtml { get; private set; }
        [DataMember(Order = 5)] public Email[] Cc { get; private set; }
        [DataMember(Order = 6)] public Email OptionalSender { get; private set; }
        [DataMember(Order = 7)] public Email OptionalReplyTo { get; private set; }
        
        MailMessageSent () 
        {
            To = new Email[0];
            Cc = new Email[0];
        }
        public MailMessageSent (Email[] to, string subject, string body, bool isHtml, Email[] cc, Email optionalSender, Email optionalReplyTo)
        {
            To = to;
            Subject = subject;
            Body = body;
            IsHtml = isHtml;
            Cc = cc;
            OptionalSender = optionalSender;
            OptionalReplyTo = optionalReplyTo;
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class EventStreamStarted : IFuncEvent
    {
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class MessageQuarantined : IFuncEvent
    {
        [DataMember(Order = 1)] public string Log { get; private set; }
        [DataMember(Order = 2)] public byte[] Envelope { get; private set; }
        [DataMember(Order = 3)] public string[] Contracts { get; private set; }
        [DataMember(Order = 4)] public DateTime TimeUtc { get; private set; }
        
        MessageQuarantined () 
        {
            Envelope = new byte[0];
            Contracts = new string[0];
        }
        public MessageQuarantined (string log, byte[] envelope, string[] contracts, DateTime timeUtc)
        {
            Log = log;
            Envelope = envelope;
            Contracts = contracts;
            TimeUtc = timeUtc;
        }
    }
    /// <summary>
    /// Create security
    /// </summary>
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class CreateSecurityAggregate : ICommand<SecurityId>
    {
        [DataMember(Order = 1)] public SecurityId Id { get; private set; }
        
        CreateSecurityAggregate () {}
        public CreateSecurityAggregate (SecurityId id)
        {
            Id = id;
        }
        
        public override string ToString()
        {
            return string.Format(@"Create security");
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class CreateSecurityFromRegistration : ICommand<SecurityId>
    {
        [DataMember(Order = 1)] public SecurityId Id { get; private set; }
        [DataMember(Order = 2)] public RegistrationId RegistrationId { get; private set; }
        [DataMember(Order = 3)] public string Login { get; private set; }
        [DataMember(Order = 4)] public string Pwd { get; private set; }
        [DataMember(Order = 5)] public string DisplayName { get; private set; }
        [DataMember(Order = 6)] public string OptionalIdentity { get; private set; }
        
        CreateSecurityFromRegistration () {}
        public CreateSecurityFromRegistration (SecurityId id, RegistrationId registrationId, string login, string pwd, string displayName, string optionalIdentity)
        {
            Id = id;
            RegistrationId = registrationId;
            Login = login;
            Pwd = pwd;
            DisplayName = displayName;
            OptionalIdentity = optionalIdentity;
        }
    }
    /// <summary>
    /// Security group created
    /// </summary>
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class SecurityAggregateCreated : IEvent<SecurityId>
    {
        [DataMember(Order = 1)] public SecurityId Id { get; private set; }
        
        SecurityAggregateCreated () {}
        public SecurityAggregateCreated (SecurityId id)
        {
            Id = id;
        }
        
        public override string ToString()
        {
            return string.Format(@"Security group created");
        }
    }
    /// <summary>
    /// Add login '{display}': {login}/{password}
    /// </summary>
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class AddSecurityPassword : ICommand<SecurityId>
    {
        [DataMember(Order = 1)] public SecurityId Id { get; private set; }
        [DataMember(Order = 2)] public string DisplayName { get; private set; }
        [DataMember(Order = 3)] public string Login { get; private set; }
        [DataMember(Order = 4)] public string Password { get; private set; }
        
        AddSecurityPassword () {}
        public AddSecurityPassword (SecurityId id, string displayName, string login, string password)
        {
            Id = id;
            DisplayName = displayName;
            Login = login;
            Password = password;
        }
        
        public override string ToString()
        {
            return string.Format(@"Add login '{0}': {1}/{2}", DisplayName, Login, Password);
        }
    }
    /// <summary>
    /// Added login '{display}'  {UserId} with encrypted pass and salt
    /// </summary>
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class SecurityPasswordAdded : IEvent<SecurityId>
    {
        [DataMember(Order = 1)] public SecurityId Id { get; private set; }
        [DataMember(Order = 2)] public UserId UserId { get; private set; }
        [DataMember(Order = 3)] public string DisplayName { get; private set; }
        [DataMember(Order = 4)] public string Login { get; private set; }
        [DataMember(Order = 5)] public string PasswordHash { get; private set; }
        [DataMember(Order = 6)] public string PasswordSalt { get; private set; }
        [DataMember(Order = 7)] public string Token { get; private set; }
        
        SecurityPasswordAdded () {}
        public SecurityPasswordAdded (SecurityId id, UserId userId, string displayName, string login, string passwordHash, string passwordSalt, string token)
        {
            Id = id;
            UserId = userId;
            DisplayName = displayName;
            Login = login;
            PasswordHash = passwordHash;
            PasswordSalt = passwordSalt;
            Token = token;
        }
        
        public override string ToString()
        {
            return string.Format(@"Added login '{1}'  {0} with encrypted pass and salt", UserId, DisplayName);
        }
    }
    /// <summary>
    /// Add identity '{display}': {identity}
    /// </summary>
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class AddSecurityIdentity : ICommand<SecurityId>
    {
        [DataMember(Order = 1)] public SecurityId Id { get; private set; }
        [DataMember(Order = 2)] public string DisplayName { get; private set; }
        [DataMember(Order = 3)] public string Identity { get; private set; }
        
        AddSecurityIdentity () {}
        public AddSecurityIdentity (SecurityId id, string displayName, string identity)
        {
            Id = id;
            DisplayName = displayName;
            Identity = identity;
        }
        
        public override string ToString()
        {
            return string.Format(@"Add identity '{0}': {1}", DisplayName, Identity);
        }
    }
    /// <summary>
    /// Added identity '{display}' {user}
    /// </summary>
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class SecurityIdentityAdded : IEvent<SecurityId>
    {
        [DataMember(Order = 1)] public SecurityId Id { get; private set; }
        [DataMember(Order = 2)] public UserId UserId { get; private set; }
        [DataMember(Order = 3)] public string DisplayName { get; private set; }
        [DataMember(Order = 4)] public string Identity { get; private set; }
        [DataMember(Order = 5)] public string Token { get; private set; }
        
        SecurityIdentityAdded () {}
        public SecurityIdentityAdded (SecurityId id, UserId userId, string displayName, string identity, string token)
        {
            Id = id;
            UserId = userId;
            DisplayName = displayName;
            Identity = identity;
            Token = token;
        }
        
        public override string ToString()
        {
            return string.Format(@"Added identity '{1}' {0}", UserId, DisplayName);
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class RemoveSecurityItem : ICommand<SecurityId>
    {
        [DataMember(Order = 1)] public SecurityId Id { get; private set; }
        [DataMember(Order = 2)] public UserId UserId { get; private set; }
        
        RemoveSecurityItem () {}
        public RemoveSecurityItem (SecurityId id, UserId userId)
        {
            Id = id;
            UserId = userId;
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class SecurityItemRemoved : IEvent<SecurityId>
    {
        [DataMember(Order = 1)] public SecurityId Id { get; private set; }
        [DataMember(Order = 2)] public UserId UserId { get; private set; }
        [DataMember(Order = 3)] public string Lookup { get; private set; }
        [DataMember(Order = 4)] public string Type { get; private set; }
        
        SecurityItemRemoved () {}
        public SecurityItemRemoved (SecurityId id, UserId userId, string lookup, string type)
        {
            Id = id;
            UserId = userId;
            Lookup = lookup;
            Type = type;
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class UpdateSecurityItemDisplayName : ICommand<SecurityId>
    {
        [DataMember(Order = 1)] public SecurityId Id { get; private set; }
        [DataMember(Order = 2)] public UserId UserId { get; private set; }
        [DataMember(Order = 3)] public string DisplayName { get; private set; }
        
        UpdateSecurityItemDisplayName () {}
        public UpdateSecurityItemDisplayName (SecurityId id, UserId userId, string displayName)
        {
            Id = id;
            UserId = userId;
            DisplayName = displayName;
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class SecurityItemDisplayNameUpdated : IEvent<SecurityId>
    {
        [DataMember(Order = 1)] public SecurityId Id { get; private set; }
        [DataMember(Order = 2)] public UserId UserId { get; private set; }
        [DataMember(Order = 3)] public string DisplayName { get; private set; }
        
        SecurityItemDisplayNameUpdated () {}
        public SecurityItemDisplayNameUpdated (SecurityId id, UserId userId, string displayName)
        {
            Id = id;
            UserId = userId;
            DisplayName = displayName;
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class SecurityRegistrationProcessCompleted : IEvent<SecurityId>
    {
        [DataMember(Order = 1)] public SecurityId Id { get; private set; }
        [DataMember(Order = 2)] public string DisplayName { get; private set; }
        [DataMember(Order = 3)] public UserId UserId { get; private set; }
        [DataMember(Order = 4)] public string Token { get; private set; }
        [DataMember(Order = 5)] public RegistrationId RegistrationId { get; private set; }
        
        SecurityRegistrationProcessCompleted () {}
        public SecurityRegistrationProcessCompleted (SecurityId id, string displayName, UserId userId, string token, RegistrationId registrationId)
        {
            Id = id;
            DisplayName = displayName;
            UserId = userId;
            Token = token;
            RegistrationId = registrationId;
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class AddPermissionToSecurityItem : ICommand<SecurityId>
    {
        [DataMember(Order = 1)] public SecurityId Id { get; private set; }
        [DataMember(Order = 2)] public UserId UserId { get; private set; }
        [DataMember(Order = 3)] public string Permission { get; private set; }
        
        AddPermissionToSecurityItem () {}
        public AddPermissionToSecurityItem (SecurityId id, UserId userId, string permission)
        {
            Id = id;
            UserId = userId;
            Permission = permission;
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class PermissionAddedToSecurityItem : IEvent<SecurityId>
    {
        [DataMember(Order = 1)] public SecurityId Id { get; private set; }
        [DataMember(Order = 2)] public UserId UserId { get; private set; }
        [DataMember(Order = 3)] public string DisplayName { get; private set; }
        [DataMember(Order = 4)] public string Permission { get; private set; }
        [DataMember(Order = 5)] public string Token { get; private set; }
        
        PermissionAddedToSecurityItem () {}
        public PermissionAddedToSecurityItem (SecurityId id, UserId userId, string displayName, string permission, string token)
        {
            Id = id;
            UserId = userId;
            DisplayName = displayName;
            Permission = permission;
            Token = token;
        }
    }
    /// <summary>
    /// Create user {id} for security {security}.
    /// </summary>
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class CreateUser : ICommand<UserId>
    {
        [DataMember(Order = 1)] public UserId Id { get; private set; }
        [DataMember(Order = 2)] public SecurityId SecurityId { get; private set; }
        
        CreateUser () {}
        public CreateUser (UserId id, SecurityId securityId)
        {
            Id = id;
            SecurityId = securityId;
        }
        
        public override string ToString()
        {
            return string.Format(@"Create user {0} for security {1}.", Id, SecurityId);
        }
    }
    /// <summary>
    /// Created user {id} ({security}) with threshold {activityThreshold}
    /// </summary>
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class UserCreated : IEvent<UserId>
    {
        [DataMember(Order = 1)] public UserId Id { get; private set; }
        [DataMember(Order = 2)] public SecurityId SecurityId { get; private set; }
        [DataMember(Order = 3)] public TimeSpan ActivityThreshold { get; private set; }
        
        UserCreated () {}
        public UserCreated (UserId id, SecurityId securityId, TimeSpan activityThreshold)
        {
            Id = id;
            SecurityId = securityId;
            ActivityThreshold = activityThreshold;
        }
        
        public override string ToString()
        {
            return string.Format(@"Created user {0} ({1}) with threshold {2}", Id, SecurityId, ActivityThreshold);
        }
    }
    /// <summary>
    /// Report login failure for user {Id} at {timeUtc}
    /// </summary>
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class ReportUserLoginFailure : ICommand<UserId>
    {
        [DataMember(Order = 1)] public UserId Id { get; private set; }
        [DataMember(Order = 2)] public DateTime TimeUtc { get; private set; }
        [DataMember(Order = 3)] public string Ip { get; private set; }
        
        ReportUserLoginFailure () {}
        public ReportUserLoginFailure (UserId id, DateTime timeUtc, string ip)
        {
            Id = id;
            TimeUtc = timeUtc;
            Ip = ip;
        }
        
        public override string ToString()
        {
            return string.Format(@"Report login failure for user {0} at {1}", Id, TimeUtc);
        }
    }
    /// <summary>
    /// User {id} login failed at {timeUtc} (via IP '{ip}')
    /// </summary>
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class UserLoginFailureReported : IEvent<UserId>
    {
        [DataMember(Order = 1)] public UserId Id { get; private set; }
        [DataMember(Order = 2)] public DateTime TimeUtc { get; private set; }
        [DataMember(Order = 3)] public SecurityId SecurityId { get; private set; }
        [DataMember(Order = 4)] public string Ip { get; private set; }
        
        UserLoginFailureReported () {}
        public UserLoginFailureReported (UserId id, DateTime timeUtc, SecurityId securityId, string ip)
        {
            Id = id;
            TimeUtc = timeUtc;
            SecurityId = securityId;
            Ip = ip;
        }
        
        public override string ToString()
        {
            return string.Format(@"User {0} login failed at {1} (via IP '{2}')", Id, TimeUtc, Ip);
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class ReportUserLoginSuccess : ICommand<UserId>
    {
        [DataMember(Order = 1)] public UserId Id { get; private set; }
        [DataMember(Order = 2)] public DateTime TimeUtc { get; private set; }
        [DataMember(Order = 3)] public string Ip { get; private set; }
        
        ReportUserLoginSuccess () {}
        public ReportUserLoginSuccess (UserId id, DateTime timeUtc, string ip)
        {
            Id = id;
            TimeUtc = timeUtc;
            Ip = ip;
        }
    }
    /// <summary>
    /// User {Id} logged in at {timeUtc} (via IP '{ip}')
    /// </summary>
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class UserLoginSuccessReported : IEvent<UserId>
    {
        [DataMember(Order = 1)] public UserId Id { get; private set; }
        [DataMember(Order = 2)] public DateTime TimeUtc { get; private set; }
        [DataMember(Order = 3)] public SecurityId SecurityId { get; private set; }
        [DataMember(Order = 4)] public string Ip { get; private set; }
        
        UserLoginSuccessReported () {}
        public UserLoginSuccessReported (UserId id, DateTime timeUtc, SecurityId securityId, string ip)
        {
            Id = id;
            TimeUtc = timeUtc;
            SecurityId = securityId;
            Ip = ip;
        }
        
        public override string ToString()
        {
            return string.Format(@"User {0} logged in at {1} (via IP '{2}')", Id, TimeUtc, Ip);
        }
    }
    /// <summary>
    /// Lock user {Id} with reason '{LockReason}'
    /// </summary>
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class LockUser : ICommand<UserId>
    {
        [DataMember(Order = 1)] public UserId Id { get; private set; }
        [DataMember(Order = 2)] public string LockReason { get; private set; }
        
        LockUser () {}
        public LockUser (UserId id, string lockReason)
        {
            Id = id;
            LockReason = lockReason;
        }
        
        public override string ToString()
        {
            return string.Format(@"Lock user {0} with reason '{1}'", Id, LockReason);
        }
    }
    /// <summary>
    /// User {Id} locked with reason '{LockReason}'.
    /// </summary>
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class UserLocked : IEvent<UserId>
    {
        [DataMember(Order = 1)] public UserId Id { get; private set; }
        [DataMember(Order = 2)] public string LockReason { get; private set; }
        [DataMember(Order = 3)] public SecurityId SecurityId { get; private set; }
        [DataMember(Order = 4)] public DateTime LockedTillUtc { get; private set; }
        
        UserLocked () {}
        public UserLocked (UserId id, string lockReason, SecurityId securityId, DateTime lockedTillUtc)
        {
            Id = id;
            LockReason = lockReason;
            SecurityId = securityId;
            LockedTillUtc = lockedTillUtc;
        }
        
        public override string ToString()
        {
            return string.Format(@"User {0} locked with reason '{1}'.", Id, LockReason);
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class UnlockUser : ICommand<UserId>
    {
        [DataMember(Order = 1)] public UserId Id { get; private set; }
        [DataMember(Order = 2)] public string UnlockReason { get; private set; }
        
        UnlockUser () {}
        public UnlockUser (UserId id, string unlockReason)
        {
            Id = id;
            UnlockReason = unlockReason;
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class UserUnlocked : IEvent<UserId>
    {
        [DataMember(Order = 1)] public UserId Id { get; private set; }
        [DataMember(Order = 2)] public string UnlockReason { get; private set; }
        [DataMember(Order = 3)] public SecurityId SecurityId { get; private set; }
        
        UserUnlocked () {}
        public UserUnlocked (UserId id, string unlockReason, SecurityId securityId)
        {
            Id = id;
            UnlockReason = unlockReason;
            SecurityId = securityId;
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class DeleteUser : ICommand<UserId>
    {
        [DataMember(Order = 1)] public UserId Id { get; private set; }
        
        DeleteUser () {}
        public DeleteUser (UserId id)
        {
            Id = id;
        }
    }
    /// <summary>
    /// Deleted user {Id} from security {SecurityId}
    /// </summary>
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class UserDeleted : IEvent<UserId>
    {
        [DataMember(Order = 1)] public UserId Id { get; private set; }
        [DataMember(Order = 2)] public SecurityId SecurityId { get; private set; }
        
        UserDeleted () {}
        public UserDeleted (UserId id, SecurityId securityId)
        {
            Id = id;
            SecurityId = securityId;
        }
        
        public override string ToString()
        {
            return string.Format(@"Deleted user {0} from security {1}", Id, SecurityId);
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class RegistrationHttpHeader
    {
        [DataMember(Order = 1)] public string Key { get; private set; }
        [DataMember(Order = 2)] public string Value { get; private set; }
        
        RegistrationHttpHeader () {}
        public RegistrationHttpHeader (string key, string value)
        {
            Key = key;
            Value = value;
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class RegistrationInfo
    {
        [DataMember(Order = 1)] public string ContactEmail { get; private set; }
        [DataMember(Order = 2)] public string CustomerName { get; private set; }
        [DataMember(Order = 3)] public string OptionalUserIdentity { get; private set; }
        [DataMember(Order = 4)] public string OptionalUserPassword { get; private set; }
        [DataMember(Order = 5)] public string OptionalCompanyPhone { get; private set; }
        [DataMember(Order = 6)] public string OptionalCompanyUrl { get; private set; }
        [DataMember(Order = 7)] public string OptionalUserDisplay { get; private set; }
        [DataMember(Order = 8)] public RegistrationHttpHeader[] Headers { get; private set; }
        [DataMember(Order = 9)] public DateTime CreatedUtc { get; private set; }
        
        RegistrationInfo () 
        {
            Headers = new RegistrationHttpHeader[0];
        }
        public RegistrationInfo (string contactEmail, string customerName, string optionalUserIdentity, string optionalUserPassword, string optionalCompanyPhone, string optionalCompanyUrl, string optionalUserDisplay, RegistrationHttpHeader[] headers, DateTime createdUtc)
        {
            ContactEmail = contactEmail;
            CustomerName = customerName;
            OptionalUserIdentity = optionalUserIdentity;
            OptionalUserPassword = optionalUserPassword;
            OptionalCompanyPhone = optionalCompanyPhone;
            OptionalCompanyUrl = optionalCompanyUrl;
            OptionalUserDisplay = optionalUserDisplay;
            Headers = headers;
            CreatedUtc = createdUtc;
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class CreateRegistration : ICommand<RegistrationId>
    {
        [DataMember(Order = 1)] public RegistrationId Id { get; private set; }
        [DataMember(Order = 2)] public RegistrationInfo Info { get; private set; }
        
        CreateRegistration () {}
        public CreateRegistration (RegistrationId id, RegistrationInfo info)
        {
            Id = id;
            Info = info;
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class RegistrationFailed : IEvent<RegistrationId>
    {
        [DataMember(Order = 1)] public RegistrationId Id { get; private set; }
        [DataMember(Order = 2)] public RegistrationInfo Info { get; private set; }
        [DataMember(Order = 3)] public string[] Problems { get; private set; }
        
        RegistrationFailed () 
        {
            Problems = new string[0];
        }
        public RegistrationFailed (RegistrationId id, RegistrationInfo info, string[] problems)
        {
            Id = id;
            Info = info;
            Problems = problems;
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class RegistrationCreated : IEvent<RegistrationId>
    {
        [DataMember(Order = 1)] public RegistrationId Id { get; private set; }
        [DataMember(Order = 2)] public DateTime RegisteredUtc { get; private set; }
        [DataMember(Order = 3)] public CustomerInfo Customer { get; private set; }
        [DataMember(Order = 4)] public SecurityInfo Security { get; private set; }
        
        RegistrationCreated () {}
        public RegistrationCreated (RegistrationId id, DateTime registeredUtc, CustomerInfo customer, SecurityInfo security)
        {
            Id = id;
            RegisteredUtc = registeredUtc;
            Customer = customer;
            Security = security;
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class CustomerInfo
    {
        [DataMember(Order = 1)] public CustomerId CustomerId { get; private set; }
        [DataMember(Order = 2)] public string CompanyName { get; private set; }
        [DataMember(Order = 3)] public string RealName { get; private set; }
        [DataMember(Order = 4)] public string CustomerEmail { get; private set; }
        [DataMember(Order = 5)] public string OptionalPhone { get; private set; }
        [DataMember(Order = 6)] public string OptionalUrl { get; private set; }
        
        CustomerInfo () {}
        public CustomerInfo (CustomerId customerId, string companyName, string realName, string customerEmail, string optionalPhone, string optionalUrl)
        {
            CustomerId = customerId;
            CompanyName = companyName;
            RealName = realName;
            CustomerEmail = customerEmail;
            OptionalPhone = optionalPhone;
            OptionalUrl = optionalUrl;
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class SecurityInfo
    {
        [DataMember(Order = 1)] public SecurityId SecurityId { get; private set; }
        [DataMember(Order = 2)] public string Login { get; private set; }
        [DataMember(Order = 3)] public string Pwd { get; private set; }
        [DataMember(Order = 4)] public string UserDisplay { get; private set; }
        [DataMember(Order = 5)] public string OptionalIdentity { get; private set; }
        
        SecurityInfo () {}
        public SecurityInfo (SecurityId securityId, string login, string pwd, string userDisplay, string optionalIdentity)
        {
            SecurityId = securityId;
            Login = login;
            Pwd = pwd;
            UserDisplay = userDisplay;
            OptionalIdentity = optionalIdentity;
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class AttachUserToRegistration : ICommand<RegistrationId>
    {
        [DataMember(Order = 1)] public RegistrationId Id { get; private set; }
        [DataMember(Order = 2)] public UserId UserId { get; private set; }
        [DataMember(Order = 3)] public string UserDisplay { get; private set; }
        [DataMember(Order = 4)] public string Token { get; private set; }
        
        AttachUserToRegistration () {}
        public AttachUserToRegistration (RegistrationId id, UserId userId, string userDisplay, string token)
        {
            Id = id;
            UserId = userId;
            UserDisplay = userDisplay;
            Token = token;
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class UserAttachedToRegistration : IEvent<RegistrationId>
    {
        [DataMember(Order = 1)] public RegistrationId Id { get; private set; }
        [DataMember(Order = 2)] public UserId UserId { get; private set; }
        [DataMember(Order = 3)] public string UserDisplay { get; private set; }
        [DataMember(Order = 4)] public string Token { get; private set; }
        
        UserAttachedToRegistration () {}
        public UserAttachedToRegistration (RegistrationId id, UserId userId, string userDisplay, string token)
        {
            Id = id;
            UserId = userId;
            UserDisplay = userDisplay;
            Token = token;
        }
    }
    [DataContract(Namespace = "Lokad.SaaS")]
    public partial class RegistrationSucceeded : IEvent<RegistrationId>
    {
        [DataMember(Order = 1)] public RegistrationId Id { get; private set; }
        [DataMember(Order = 2)] public CustomerId CustomerId { get; private set; }
        [DataMember(Order = 3)] public SecurityId SecurityId { get; private set; }
        [DataMember(Order = 4)] public UserId UserId { get; private set; }
        [DataMember(Order = 5)] public string UserDisplayName { get; private set; }
        [DataMember(Order = 6)] public string UserToken { get; private set; }
        
        RegistrationSucceeded () {}
        public RegistrationSucceeded (RegistrationId id, CustomerId customerId, SecurityId securityId, UserId userId, string userDisplayName, string userToken)
        {
            Id = id;
            CustomerId = customerId;
            SecurityId = securityId;
            UserId = userId;
            UserDisplayName = userDisplayName;
            UserToken = userToken;
        }
    }
    
    public interface IRegistrationApplicationService
    {
        void When(CreateRegistration c);
        void When(AttachUserToRegistration c);
    }
    
    public interface IRegistrationState
    {
        void When(RegistrationFailed e);
        void When(RegistrationCreated e);
        void When(UserAttachedToRegistration e);
        void When(RegistrationSucceeded e);
    }
    
    public interface IUserApplicationService
    {
        void When(CreateUser c);
        void When(ReportUserLoginFailure c);
        void When(ReportUserLoginSuccess c);
        void When(LockUser c);
        void When(UnlockUser c);
        void When(DeleteUser c);
    }
    
    public interface IUserState
    {
        void When(UserCreated e);
        void When(UserLoginFailureReported e);
        void When(UserLoginSuccessReported e);
        void When(UserLocked e);
        void When(UserUnlocked e);
        void When(UserDeleted e);
    }
    
    public interface ISecurityApplicationService
    {
        void When(CreateSecurityAggregate c);
        void When(CreateSecurityFromRegistration c);
        void When(AddSecurityPassword c);
        void When(AddSecurityIdentity c);
        void When(RemoveSecurityItem c);
        void When(UpdateSecurityItemDisplayName c);
        void When(AddPermissionToSecurityItem c);
    }
    
    public interface ISecurityState
    {
        void When(SecurityAggregateCreated e);
        void When(SecurityPasswordAdded e);
        void When(SecurityIdentityAdded e);
        void When(SecurityItemRemoved e);
        void When(SecurityItemDisplayNameUpdated e);
        void When(SecurityRegistrationProcessCompleted e);
        void When(PermissionAddedToSecurityItem e);
    }
    #endregion
}
