# Settings Infrastructure Implementation Plan

## Overview
This document outlines the implementation plan for a generic, extensible settings infrastructure for the GroundUp platform. This infrastructure will support application-level, tenant-level, and user-level settings with dynamic hierarchy support, multiple definition sources, and a WordPress-style installation experience.

## Goals
- Provide a flexible settings system that can be extended by any domain
- Support hierarchical setting overrides (System ? Custom Levels ? Tenant ? User)
- Enable three ways to define settings: seeded data, code registration, and UI creation
- Implement WordPress-style first-time setup flow
- Support encrypted secrets with migration to external providers
- Build data-driven UI capabilities for dynamic settings management
- Follow existing GroundUp patterns (Repository ? Service ? Controller)

---

## Phase 1: Database Schema & Entities

### 1.1 Core Tables

#### SettingCategory
- **Purpose**: Top-level grouping for settings (e.g., Authentication, Logging, Cache)
- **Columns**:
  - `Id` (Guid, PK)
  - `Key` (string, unique) - e.g., "groundup_authentication"
  - `DisplayName` (string) - e.g., "Authentication"
  - `Description` (string, nullable)
  - `Icon` (string, nullable) - UI hint for icon
  - `SortOrder` (int) - Display order in UI
  - `IsActive` (bool) - Can be disabled without deletion
  - `CreatedBy` (Guid, nullable)
  - `CreatedAt` (DateTime)
  - `ModifiedBy` (Guid, nullable)
  - `ModifiedAt` (DateTime, nullable)

#### SettingGroup
- **Purpose**: Mid-level grouping within a category (e.g., Keycloak, Auth0 under Authentication)
- **Columns**:
  - `Id` (Guid, PK)
  - `CategoryId` (Guid, FK ? SettingCategory)
  - `Key` (string, unique) - e.g., "groundup_authentication_keycloak"
  - `DisplayName` (string) - e.g., "Keycloak Configuration"
  - `Description` (string, nullable)
  - `SortOrder` (int)
  - `IsActive` (bool)
  - `CreatedBy` (Guid, nullable)
  - `CreatedAt` (DateTime)
  - `ModifiedBy` (Guid, nullable)
  - `ModifiedAt` (DateTime, nullable)

#### SettingDefinition
- **Purpose**: Defines the schema for individual settings
- **Columns**:
  - `Id` (Guid, PK)
  - `GroupId` (Guid, FK ? SettingGroup)
  - `Key` (string, unique) - e.g., "groundup_authentication_keycloak_realm"
  - `DisplayName` (string) - e.g., "Realm Name"
  - `Description` (string, nullable)
  - `DataType` (string/enum) - String, Number, Boolean, Json, SecretString, Enum, MultiSelect, Url, Email
  - `DefaultValue` (string, JSON) - Serialized default value
  - `ValidationRules` (string, JSON) - See validation structure below
  - `UIHints` (string, JSON) - See UI hints structure below
  - `IsRequired` (bool)
  - `IsSecret` (bool) - Should be encrypted
  - `RequiresRestart` (bool) - Changes need app restart
  - `AllowOverride` (bool) - Can be overridden at lower hierarchy levels
  - `OverrideBehavior` (string/enum) - "Inherit", "ExplicitOnly", "AllowSkipLevel"
  - `SortOrder` (int)
  - `IsActive` (bool)
  - `Source` (string/enum) - "Seeded", "Code", "UI"
  - `CreatedBy` (Guid, nullable)
  - `CreatedAt` (DateTime)
  - `ModifiedBy` (Guid, nullable)
  - `ModifiedAt` (DateTime, nullable)

#### SettingHierarchyLevel
- **Purpose**: Defines custom hierarchy levels beyond System/Tenant/User
- **Columns**:
  - `Id` (Guid, PK)
  - `Key` (string, unique) - e.g., "Case", "Department", "Matter"
  - `DisplayName` (string)
  - `ParentLevelKey` (string, nullable, FK ? SettingHierarchyLevel.Key) - null for top-level
  - `EntityType` (string, nullable) - Fully qualified type name if tied to entity
  - `SortOrder` (int) - Hierarchy order (lower = higher in hierarchy)
  - `IsActive` (bool)
  - `RegisteredBy` (string) - Assembly/domain that registered it
  - `CreatedAt` (DateTime)

**Pre-seeded levels**:
- System (SortOrder: 0, ParentLevelKey: null)
- Tenant (SortOrder: 100, ParentLevelKey: "System")
- User (SortOrder: 1000, ParentLevelKey: "Tenant")

#### SettingValue
- **Purpose**: Stores actual setting values at different hierarchy levels
- **Columns**:
  - `Id` (Guid, PK)
  - `SettingDefinitionId` (Guid, FK ? SettingDefinition)
  - `HierarchyLevelKey` (string, FK ? SettingHierarchyLevel.Key)
  - `ScopeId` (Guid, nullable) - TenantId, UserId, CaseId, etc. (null for System level)
  - `Value` (string, JSON blob) - Encrypted if IsSecret
  - `EncryptionVersion` (int) - Track which encryption method was used
  - `IsOverridden` (bool) - Explicitly set at this level (vs inherited)
  - `SetBy` (Guid, nullable, FK ? User)
  - `SetAt` (DateTime)
  - `ModifiedBy` (Guid, nullable)
  - `ModifiedAt` (DateTime, nullable)

**Indexes**:
- Composite unique: (SettingDefinitionId, HierarchyLevelKey, ScopeId)
- For efficient lookups: (HierarchyLevelKey, ScopeId)

#### InstallationState
- **Purpose**: Track installation/setup completion
- **Columns**:
  - `Id` (Guid, PK)
  - `IsSetupComplete` (bool)
  - `SetupCompletedAt` (DateTime, nullable)
  - `SetupCompletedBy` (Guid, nullable)
  - `DatabaseVersion` (string) - Migration version
  - `EncryptionVersion` (int) - Current encryption method
  - `BootstrapSettingsHash` (string) - Verify bootstrap settings haven't changed
  - `LastHealthCheck` (DateTime)

### 1.2 Validation Rules JSON Structure
```json
{
  "rules": [
    {
      "type": "required",
      "message": "This field is required"
    },
    {
      "type": "minLength",
      "value": 3,
      "message": "Must be at least 3 characters"
    },
    {
      "type": "maxLength",
      "value": 500,
      "message": "Must not exceed 500 characters"
    },
    {
      "type": "pattern",
      "value": "^https?://.*",
      "message": "Must be a valid HTTP(S) URL"
    },
    {
      "type": "range",
      "min": 1,
      "max": 100,
      "message": "Must be between 1 and 100"
    },
    {
      "type": "enum",
      "values": ["option1", "option2", "option3"],
      "message": "Must be one of the allowed values"
    },
    {
      "type": "custom",
      "validatorClass": "MyApp.Validators.CustomSettingValidator",
      "message": "Custom validation failed"
    }
  ]
}
```

### 1.3 UI Hints JSON Structure
```json
{
  "controlType": "textbox|textarea|checkbox|dropdown|multiselect|number|password|colorpicker|datepicker|toggle",
  "placeholder": "Enter value...",
  "helpText": "Additional help text shown below control",
  "options": [
    {"value": "opt1", "label": "Option 1"},
    {"value": "opt2", "label": "Option 2"}
  ],
  "width": "full|half|third",
  "rows": 5,
  "showWhen": {
    "settingKey": "groundup_authentication_provider",
    "equals": "keycloak"
  }
}
```

---

## Phase 2: Domain Models & DTOs

### 2.1 Entity Models

Create entity classes in `GroundUp.Data.Abstractions/Entities/Settings/`:
- `SettingCategory.cs`
- `SettingGroup.cs`
- `SettingDefinition.cs`
- `SettingHierarchyLevel.cs`
- `SettingValue.cs`
- `InstallationState.cs`

### 2.2 DTOs

Create DTOs in `GroundUp.Core/Dtos/Settings/`:

**Request DTOs**:
- `CreateSettingCategoryDto`
- `UpdateSettingCategoryDto`
- `CreateSettingGroupDto`
- `UpdateSettingGroupDto`
- `CreateSettingDefinitionDto`
- `UpdateSettingDefinitionDto`
- `SetSettingValueDto` - For setting a value at a specific hierarchy level
- `RegisterHierarchyLevelDto`
- `BootstrapSettingsDto` - For initial setup
- `EncryptionMigrationDto`

**Response DTOs**:
- `SettingCategoryDto`
- `SettingGroupDto`
- `SettingDefinitionDto`
- `SettingValueDto`
- `ResolvedSettingDto` - Includes resolved value and which level it came from
- `SettingHierarchyLevelDto`
- `InstallationStateDto`
- `SettingSchemaDto` - Full schema for UI building

**Filter/Query DTOs**:
- `SettingFilterParams` (extends `FilterParams`)
- `SettingValueQuery` - For querying resolved values with hierarchy context

### 2.3 Enums

Create in `GroundUp.Core/Enums/`:
```csharp
public enum SettingDataType
{
    String,
    Number,
    Boolean,
    Json,
    SecretString,
    Enum,
    MultiSelect,
    Url,
    Email
}

public enum SettingSource
{
    Seeded,
    Code,
    UI
}

public enum OverrideBehavior
{
    Inherit,           // Always walk up the chain
    ExplicitOnly,      // Only use explicitly set values
    AllowSkipLevel     // Can skip to higher level (e.g., skip Tenant, use System)
}

public enum EncryptionProvider
{
    Bootstrap,         // Initial encryption key
    AzureKeyVault,
    AwsSecretsManager,
    HashiCorpVault
}
```

---

## Phase 3: Repository Layer

### 3.1 Repository Interfaces

Create in `GroundUp.Data.Abstractions/Interfaces/`:

```csharp
public interface ISettingCategoryRepository : IBaseRepository<SettingCategory>
{
    Task<SettingCategory?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<List<SettingCategory>> GetActiveAsync(CancellationToken cancellationToken = default);
}

public interface ISettingGroupRepository : IBaseRepository<SettingGroup>
{
    Task<SettingGroup?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<List<SettingGroup>> GetByCategoryIdAsync(Guid categoryId, CancellationToken cancellationToken = default);
}

public interface ISettingDefinitionRepository : IBaseRepository<SettingDefinition>
{
    Task<SettingDefinition?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<List<SettingDefinition>> GetByGroupIdAsync(Guid groupId, CancellationToken cancellationToken = default);
    Task<List<SettingDefinition>> GetBySourceAsync(SettingSource source, CancellationToken cancellationToken = default);
    Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken = default);
    Task<List<SettingDefinition>> GetSecretsAsync(CancellationToken cancellationToken = default);
}

public interface ISettingHierarchyLevelRepository : IBaseRepository<SettingHierarchyLevel>
{
    Task<SettingHierarchyLevel?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);
    Task<List<SettingHierarchyLevel>> GetOrderedHierarchyAsync(CancellationToken cancellationToken = default);
    Task<bool> KeyExistsAsync(string key, CancellationToken cancellationToken = default);
}

public interface ISettingValueRepository : IBaseRepository<SettingValue>
{
    Task<SettingValue?> GetValueAsync(
        Guid settingDefinitionId, 
        string hierarchyLevelKey, 
        Guid? scopeId, 
        CancellationToken cancellationToken = default);
    
    Task<List<SettingValue>> GetValuesForScopeAsync(
        string hierarchyLevelKey, 
        Guid scopeId, 
        CancellationToken cancellationToken = default);
    
    Task<List<SettingValue>> GetAllValuesForSettingAsync(
        Guid settingDefinitionId, 
        CancellationToken cancellationToken = default);
    
    Task SetValueAsync(
        Guid settingDefinitionId, 
        string hierarchyLevelKey, 
        Guid? scopeId, 
        string value, 
        Guid setByUserId,
        CancellationToken cancellationToken = default);
}

public interface IInstallationStateRepository : IBaseRepository<InstallationState>
{
    Task<InstallationState?> GetCurrentStateAsync(CancellationToken cancellationToken = default);
    Task<bool> IsSetupCompleteAsync(CancellationToken cancellationToken = default);
    Task MarkSetupCompleteAsync(Guid userId, CancellationToken cancellationToken = default);
}
```

### 3.2 Repository Implementations

Create implementations in `GroundUp.Data.Core/Repositories/Settings/`:
- `SettingCategoryRepository.cs`
- `SettingGroupRepository.cs`
- `SettingDefinitionRepository.cs`
- `SettingHierarchyLevelRepository.cs`
- `SettingValueRepository.cs`
- `InstallationStateRepository.cs`

All should inherit from `BaseRepository<T>` and implement standard CRUD operations.

---

## Phase 4: Service Layer

### 4.1 Service Interfaces

Create in `GroundUp.Core/Interfaces/`:

```csharp
public interface ISettingService
{
    // Setting Definition Management
    Task<SettingDefinitionDto> CreateDefinitionAsync(CreateSettingDefinitionDto dto, CancellationToken cancellationToken = default);
    Task<SettingDefinitionDto> UpdateDefinitionAsync(Guid id, UpdateSettingDefinitionDto dto, CancellationToken cancellationToken = default);
    Task DeleteDefinitionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SettingDefinitionDto?> GetDefinitionAsync(Guid id, CancellationToken cancellationToken = default);
    Task<SettingDefinitionDto?> GetDefinitionByKeyAsync(string key, CancellationToken cancellationToken = default);
    
    // Category & Group Management
    Task<SettingCategoryDto> CreateCategoryAsync(CreateSettingCategoryDto dto, CancellationToken cancellationToken = default);
    Task<SettingGroupDto> CreateGroupAsync(CreateSettingGroupDto dto, CancellationToken cancellationToken = default);
    Task<List<SettingCategoryDto>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default);
    
    // Value Resolution (with hierarchy walking)
    Task<ResolvedSettingDto?> GetResolvedValueAsync(
        string settingKey, 
        SettingResolutionContext context, 
        CancellationToken cancellationToken = default);
    
    Task<T?> GetResolvedValueAsync<T>(
        string settingKey, 
        SettingResolutionContext context, 
        CancellationToken cancellationToken = default);
    
    // Value Setting
    Task SetValueAsync(
        string settingKey, 
        string hierarchyLevelKey, 
        Guid? scopeId, 
        object value, 
        CancellationToken cancellationToken = default);
    
    // Schema for UI
    Task<SettingSchemaDto> GetSchemaAsync(CancellationToken cancellationToken = default);
    
    // Validation
    Task<ValidationResult> ValidateValueAsync(string settingKey, object value, CancellationToken cancellationToken = default);
}

public interface ISettingHierarchyService
{
    Task RegisterHierarchyLevelAsync(RegisterHierarchyLevelDto dto, CancellationToken cancellationToken = default);
    Task<List<SettingHierarchyLevelDto>> GetOrderedHierarchyAsync(CancellationToken cancellationToken = default);
    Task<List<string>> BuildResolutionChainAsync(SettingResolutionContext context, CancellationToken cancellationToken = default);
}

public interface ISettingEncryptionService
{
    Task<string> EncryptAsync(string plainText, CancellationToken cancellationToken = default);
    Task<string> DecryptAsync(string encryptedText, int encryptionVersion, CancellationToken cancellationToken = default);
    Task MigrateEncryptionAsync(EncryptionMigrationDto dto, CancellationToken cancellationToken = default);
}

public interface ISettingRegistrationService
{
    Task RegisterSettingsAsync(ISettingDefinitionBuilder builder, CancellationToken cancellationToken = default);
    Task SyncCodeRegisteredSettingsAsync(CancellationToken cancellationToken = default);
}

public interface IInstallationService
{
    Task<bool> IsSetupCompleteAsync(CancellationToken cancellationToken = default);
    Task<InstallationStateDto> GetInstallationStateAsync(CancellationToken cancellationToken = default);
    Task CompleteBootstrapAsync(BootstrapSettingsDto dto, CancellationToken cancellationToken = default);
}
```

**Support Classes**:
```csharp
public class SettingResolutionContext
{
    public Guid? UserId { get; set; }
    public Guid? TenantId { get; set; }
    public Dictionary<string, Guid> CustomScopes { get; set; } = new(); // e.g., {"Case": caseId}
}

public interface ISettingDefinitionBuilder
{
    ISettingDefinitionBuilder AddCategory(string key, string displayName, Action<ISettingCategoryBuilder> configure = null);
}

public interface ISettingCategoryBuilder
{
    ISettingCategoryBuilder AddGroup(string key, string displayName, Action<ISettingGroupBuilder> configure = null);
}

public interface ISettingGroupBuilder
{
    ISettingGroupBuilder AddSetting(string key, SettingDataType dataType, Action<ISettingBuilder> configure = null);
}

public interface ISettingBuilder
{
    ISettingBuilder WithDisplayName(string displayName);
    ISettingBuilder WithDescription(string description);
    ISettingBuilder WithDefaultValue(object value);
    ISettingBuilder IsRequired();
    ISettingBuilder IsSecret();
    ISettingBuilder RequiresRestart();
    ISettingBuilder AddValidation(string type, object value, string message);
    ISettingBuilder WithUIHints(object hints);
}
```

### 4.2 Service Implementations

Create in `GroundUp.Services.Core/Settings/`:
- `SettingService.cs`
- `SettingHierarchyService.cs`
- `SettingEncryptionService.cs`
- `SettingRegistrationService.cs`
- `InstallationService.cs`

**Key Logic in SettingService.GetResolvedValueAsync**:
```csharp
public async Task<ResolvedSettingDto?> GetResolvedValueAsync(
    string settingKey, 
    SettingResolutionContext context, 
    CancellationToken cancellationToken = default)
{
    // 1. Get setting definition
    var definition = await _settingDefinitionRepository.GetByKeyAsync(settingKey, cancellationToken);
    if (definition == null) return null;
    
    // 2. Build resolution chain based on context
    var resolutionChain = await _hierarchyService.BuildResolutionChainAsync(context, cancellationToken);
    
    // 3. Walk chain from bottom to top (User ? Tenant ? System)
    foreach (var (levelKey, scopeId) in resolutionChain.Reverse())
    {
        var value = await _settingValueRepository.GetValueAsync(
            definition.Id, 
            levelKey, 
            scopeId, 
            cancellationToken);
        
        if (value != null && value.IsOverridden)
        {
            // Check override behavior
            if (definition.OverrideBehavior == OverrideBehavior.AllowSkipLevel)
            {
                // Check if value explicitly says "skip to parent"
                if (IsSkipToParentValue(value.Value))
                    continue;
            }
            
            // Decrypt if secret
            var resolvedValue = definition.IsSecret 
                ? await _encryptionService.DecryptAsync(value.Value, value.EncryptionVersion, cancellationToken)
                : value.Value;
            
            return new ResolvedSettingDto
            {
                SettingKey = settingKey,
                Value = resolvedValue,
                ResolvedAt = levelKey,
                ResolvedScopeId = scopeId,
                DataType = definition.DataType
            };
        }
    }
    
    // 4. Return default value if no override found
    return new ResolvedSettingDto
    {
        SettingKey = settingKey,
        Value = definition.DefaultValue,
        ResolvedAt = "Default",
        DataType = definition.DataType
    };
}
```

---

## Phase 5: API Layer

### 5.1 Controllers

Create in `GroundUp.Api/Controllers/`:

```csharp
[ApiController]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    // GET api/settings/schema - Get full schema for UI building
    // GET api/settings/categories - Get all categories
    // POST api/settings/categories - Create category
    // GET api/settings/groups/{categoryId} - Get groups by category
    // POST api/settings/groups - Create group
    // GET api/settings/definitions/{groupId} - Get definitions by group
    // GET api/settings/definitions/{key} - Get definition by key
    // POST api/settings/definitions - Create definition
    // PUT api/settings/definitions/{id} - Update definition
    // DELETE api/settings/definitions/{id} - Delete definition
    // GET api/settings/values/{key} - Get resolved value with context
    // POST api/settings/values - Set value at hierarchy level
    // POST api/settings/validate - Validate a value against a definition
}

[ApiController]
[Route("api/settings/hierarchy")]
public class SettingHierarchyController : ControllerBase
{
    // GET api/settings/hierarchy - Get ordered hierarchy levels
    // POST api/settings/hierarchy - Register new hierarchy level
}

[ApiController]
[Route("api/settings/encryption")]
public class SettingEncryptionController : ControllerBase
{
    // POST api/settings/encryption/migrate - Migrate to new encryption provider
    // GET api/settings/encryption/status - Get encryption status
}

[ApiController]
[Route("api/setup")]
public class SetupController : ControllerBase
{
    // GET api/setup/status - Check if setup is complete
    // POST api/setup/bootstrap - Complete initial bootstrap
    // Should only work if setup is not complete
}
```

### 5.2 Middleware

Create `SetupCheckMiddleware.cs` in `GroundUp.Api/Middleware/`:
- Intercepts all requests (except `/api/setup/*`)
- Checks `IInstallationService.IsSetupCompleteAsync()`
- If not complete, returns 503 Service Unavailable with message
- Allows setup endpoints to pass through

### 5.3 Permission Integration

Apply existing permission interceptor pattern:
- `SettingsController` methods decorated with permission requirements
- Example: `[RequirePermission("Settings.Admin")]` or `[RequirePermission("Settings.Read")]`
- Tenant-level settings require tenant context
- User-level settings require user context

---

## Phase 6: Bootstrap & Installation Flow

### 6.1 Bootstrap Settings Storage

Create `BootstrapSettingsManager.cs`:
- Stores bootstrap settings in-memory during setup
- Exposes: `DatabaseConnectionString`, `EncryptionKey`, `InitialAdminUser`
- After bootstrap complete, these become regular settings in DB

### 6.2 Installation Detection

**IsSetupRequired Logic**:
1. Check if `BootstrapSettingsManager` has connection string
   - If no ? Setup required
2. Try to connect to database
   - If fails ? Setup required
3. Check if `InstallationState` table exists
   - If no ? Setup required
4. Query `InstallationState.IsSetupComplete`
   - If false ? Setup required
5. Otherwise ? Setup complete

### 6.3 Setup Flow

1. **User navigates to app** ? Redirected to `/setup`
2. **Bootstrap form** collects:
   - Database connection string
   - Encryption key
   - Initial admin credentials (username, email, password)
3. **POST /api/setup/bootstrap**:
   - Stores settings in `BootstrapSettingsManager`
   - Runs database migrations
   - Seeds core setting definitions
   - Creates initial admin user
   - Marks setup as complete
   - Returns success
4. **UI shows loading screen** while backend initializes
5. **Setup complete** ? Redirect to login
6. **Admin logs in** ? Configures remaining settings through UI

### 6.4 Core Seeded Settings

Define in migration or seed data:

**Category: groundup_core**
- Group: groundup_core_application
  - groundup_core_application_mode (Enum: MultiTenant, SingleTenant)
  - groundup_core_application_name (String)
  
**Category: groundup_authentication**
- Group: groundup_authentication_provider
  - groundup_authentication_provider_type (Enum: None, Forms, Keycloak, Auth0)
  - groundup_authentication_provider_keycloak_url (Url, Secret, Conditional)
  - groundup_authentication_provider_keycloak_realm (String, Conditional)
  - groundup_authentication_provider_keycloak_clientid (String, Conditional)
  - groundup_authentication_provider_keycloak_clientsecret (SecretString, Conditional)
  
**Category: groundup_logging**
- Group: groundup_logging_provider
  - groundup_logging_provider_type (Enum: Console, File, AWS, Azure)
  - groundup_logging_provider_level (Enum: Debug, Info, Warning, Error)
  - groundup_logging_provider_file_path (String, Conditional)
  
**Category: groundup_cache**
- Group: groundup_cache_configuration
  - groundup_cache_enabled (Boolean)
  - groundup_cache_provider (Enum: InMemory, Redis, Distributed)
  - groundup_cache_redis_connectionstring (String, Secret, Conditional)
  
**Category: groundup_secrets**
- Group: groundup_secrets_provider
  - groundup_secrets_provider_type (Enum: Bootstrap, AzureKeyVault, AWSSecretsManager)
  - groundup_secrets_provider_azure_vaulturl (Url, Conditional)
  - groundup_secrets_provider_aws_region (String, Conditional)

---

## Phase 7: Code Registration System

### 7.1 Registration API

Domains register settings during startup:

```csharp
public class InventorySettingsRegistration : ISettingRegistration
{
    public void Register(ISettingDefinitionBuilder builder)
    {
        builder
            .AddCategory("inventory_core", "Inventory")
            .AddGroup("inventory_core_general", "General Settings", group =>
            {
                group.AddSetting("inventory_core_general_defaultunit", SettingDataType.Enum, setting =>
                {
                    setting
                        .WithDisplayName("Default Unit of Measure")
                        .WithDescription("Default unit when creating new items")
                        .WithDefaultValue("Each")
                        .AddValidation("enum", new[] { "Each", "Box", "Pallet" }, "Must be valid unit")
                        .WithUIHints(new { controlType = "dropdown", options = new[] { "Each", "Box", "Pallet" } });
                });
            });
    }
}
```

### 7.2 DI Registration

In `ServiceCollectionExtensions.cs` for each domain:
```csharp
public static IServiceCollection AddInventorySettings(this IServiceCollection services)
{
    services.AddSingleton<ISettingRegistration, InventorySettingsRegistration>();
    return services;
}
```

### 7.3 Startup Sync

In `ApplicationBuilderExtensions.cs`:
```csharp
public static async Task SyncSettingsAsync(this IApplicationBuilder app)
{
    using var scope = app.ApplicationServices.CreateScope();
    var registrationService = scope.ServiceProvider.GetRequiredService<ISettingRegistrationService>();
    
    var registrations = scope.ServiceProvider.GetServices<ISettingRegistration>();
    
    var builder = new SettingDefinitionBuilder();
    foreach (var registration in registrations)
    {
        registration.Register(builder);
    }
    
    await registrationService.RegisterSettingsAsync(builder);
}
```

Call in `Program.cs` after `app.Run()`:
```csharp
if (await app.Services.GetRequiredService<IInstallationService>().IsSetupCompleteAsync())
{
    await app.SyncSettingsAsync();
}
```

---

## Phase 8: Dynamic Hierarchy Registration

### 8.1 Registration API

Domains can register custom hierarchy levels:

```csharp
public class CaseModule
{
    public static void RegisterHierarchy(IServiceProvider services)
    {
        var hierarchyService = services.GetRequiredService<ISettingHierarchyService>();
        
        await hierarchyService.RegisterHierarchyLevelAsync(new RegisterHierarchyLevelDto
        {
            Key = "Case",
            DisplayName = "Case",
            ParentLevelKey = "Tenant",
            EntityType = "MyApp.Entities.Case",
            SortOrder = 500 // Between Tenant (100) and User (1000)
        });
    }
}
```

### 8.2 Resolution Chain Building

`SettingHierarchyService.BuildResolutionChainAsync`:
```csharp
public async Task<List<(string LevelKey, Guid? ScopeId)>> BuildResolutionChainAsync(
    SettingResolutionContext context, 
    CancellationToken cancellationToken = default)
{
    var chain = new List<(string LevelKey, Guid? ScopeId)>();
    
    // Always start with System
    chain.Add(("System", null));
    
    // Add Tenant if available
    if (context.TenantId.HasValue)
        chain.Add(("Tenant", context.TenantId));
    
    // Get ordered hierarchy and add custom levels
    var hierarchy = await GetOrderedHierarchyAsync(cancellationToken);
    
    foreach (var level in hierarchy.Where(l => l.SortOrder > 100 && l.SortOrder < 1000))
    {
        if (context.CustomScopes.TryGetValue(level.Key, out var scopeId))
        {
            chain.Add((level.Key, scopeId));
        }
    }
    
    // Add User if available
    if (context.UserId.HasValue)
        chain.Add(("User", context.UserId));
    
    return chain;
}
```

---

## Phase 9: Encryption & Security

### 9.1 Encryption Service Implementation

```csharp
public class SettingEncryptionService : ISettingEncryptionService
{
    private readonly IConfiguration _configuration;
    private readonly IInstallationStateRepository _installationStateRepository;
    
    public async Task<string> EncryptAsync(string plainText, CancellationToken cancellationToken = default)
    {
        var state = await _installationStateRepository.GetCurrentStateAsync(cancellationToken);
        
        switch (state?.EncryptionVersion)
        {
            case 0: // Bootstrap encryption
                return EncryptWithBootstrapKey(plainText);
            case 1: // Azure Key Vault
                return await EncryptWithAzureKeyVault(plainText);
            // ... other providers
            default:
                throw new InvalidOperationException("Unknown encryption version");
        }
    }
    
    public async Task<string> DecryptAsync(string encryptedText, int encryptionVersion, CancellationToken cancellationToken = default)
    {
        switch (encryptionVersion)
        {
            case 0:
                return DecryptWithBootstrapKey(encryptedText);
            case 1:
                return await DecryptWithAzureKeyVault(encryptedText);
            // ... other providers
            default:
                throw new InvalidOperationException($"Unknown encryption version: {encryptionVersion}");
        }
    }
    
    public async Task MigrateEncryptionAsync(EncryptionMigrationDto dto, CancellationToken cancellationToken = default)
    {
        // 1. Get all secret settings
        var secrets = await _settingDefinitionRepository.GetSecretsAsync(cancellationToken);
        
        // 2. For each, decrypt with old method, encrypt with new
        foreach (var secret in secrets)
        {
            var values = await _settingValueRepository.GetAllValuesForSettingAsync(secret.Id, cancellationToken);
            
            foreach (var value in values)
            {
                var decrypted = await DecryptAsync(value.Value, value.EncryptionVersion, cancellationToken);
                var encrypted = await EncryptAsync(decrypted, cancellationToken); // Uses new version
                
                value.Value = encrypted;
                value.EncryptionVersion = dto.NewEncryptionVersion;
                await _settingValueRepository.UpdateAsync(value, cancellationToken);
            }
        }
        
        // 3. Update installation state
        var state = await _installationStateRepository.GetCurrentStateAsync(cancellationToken);
        state.EncryptionVersion = dto.NewEncryptionVersion;
        await _installationStateRepository.UpdateAsync(state, cancellationToken);
    }
}
```

### 9.2 Secret Value Handling

When returning secret settings via API:
```csharp
public async Task<SettingValueDto> GetValueDtoAsync(SettingValue value, SettingDefinition definition)
{
    return new SettingValueDto
    {
        Id = value.Id,
        SettingKey = definition.Key,
        Value = definition.IsSecret ? "********" : value.Value, // Mask secrets
        IsSecret = definition.IsSecret,
        SetAt = value.SetAt,
        SetBy = value.SetBy
    };
}
```

---

## Phase 10: Validation System

### 10.1 Validation Service

Create `SettingValidationService.cs`:
```csharp
public class SettingValidationService
{
    public async Task<ValidationResult> ValidateAsync(
        SettingDefinition definition, 
        object value,
        CancellationToken cancellationToken = default)
    {
        var errors = new List<string>();
        
        // Parse validation rules
        var rules = JsonSerializer.Deserialize<ValidationRules>(definition.ValidationRules);
        
        foreach (var rule in rules.Rules)
        {
            switch (rule.Type)
            {
                case "required":
                    if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                        errors.Add(rule.Message ?? "This field is required");
                    break;
                    
                case "minLength":
                    if (value?.ToString()?.Length < rule.Value)
                        errors.Add(rule.Message ?? $"Must be at least {rule.Value} characters");
                    break;
                    
                case "pattern":
                    if (!Regex.IsMatch(value?.ToString() ?? "", rule.Value.ToString()))
                        errors.Add(rule.Message ?? "Invalid format");
                    break;
                    
                case "custom":
                    var validator = CreateCustomValidator(rule.ValidatorClass);
                    var customResult = await validator.ValidateAsync(value, cancellationToken);
                    if (!customResult.IsValid)
                        errors.AddRange(customResult.Errors);
                    break;
                    
                // ... other validation types
            }
        }
        
        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }
}
```

---

## Phase 11: UI Data Contract

### 11.1 Schema Endpoint Response

`GET /api/settings/schema` returns:
```json
{
  "categories": [
    {
      "id": "guid",
      "key": "groundup_authentication",
      "displayName": "Authentication",
      "description": "Configure authentication providers",
      "icon": "shield",
      "sortOrder": 1,
      "groups": [
        {
          "id": "guid",
          "key": "groundup_authentication_provider",
          "displayName": "Provider Configuration",
          "sortOrder": 1,
          "settings": [
            {
              "id": "guid",
              "key": "groundup_authentication_provider_type",
              "displayName": "Provider Type",
              "description": "Choose your authentication provider",
              "dataType": "Enum",
              "defaultValue": "None",
              "isRequired": true,
              "isSecret": false,
              "requiresRestart": true,
              "validationRules": { /* ... */ },
              "uiHints": {
                "controlType": "dropdown",
                "options": [
                  {"value": "None", "label": "No Authentication"},
                  {"value": "Keycloak", "label": "Keycloak"},
                  {"value": "Auth0", "label": "Auth0"}
                ]
              },
              "currentValue": "Keycloak", // If set
              "resolvedAt": "System", // or "Tenant", "User", etc.
              "canOverride": true
            }
          ]
        }
      ]
    }
  ],
  "hierarchyLevels": [
    {"key": "System", "displayName": "System", "sortOrder": 0},
    {"key": "Tenant", "displayName": "Tenant", "sortOrder": 100},
    {"key": "User", "displayName": "User", "sortOrder": 1000}
  ]
}
```

### 11.2 UI Dynamic Rendering

Frontend can:
1. Fetch schema
2. Build tabs for categories
3. Build accordions/sections for groups
4. Render form controls based on `dataType` and `uiHints`
5. Apply validation rules client-side
6. Show/hide conditional settings based on `showWhen`
7. Display current values and resolution level
8. Allow override at appropriate hierarchy level

---

## Phase 12: Caching Strategy (Future)

### 12.1 Cache Service Interface
```csharp
public interface ISettingCacheService
{
    Task<ResolvedSettingDto?> GetCachedValueAsync(string key, SettingResolutionContext context);
    Task SetCachedValueAsync(string key, SettingResolutionContext context, ResolvedSettingDto value, TimeSpan? expiration = null);
    Task InvalidateCacheAsync(string settingKey);
    Task InvalidateAllCacheAsync();
}
```

### 12.2 Cache Invalidation

When setting value changes:
- Invalidate cache for that setting key
- If cached in distributed cache, broadcast invalidation message
- If `RequiresRestart`, log warning and potentially notify admins

---

## Phase 13: Testing Strategy

### 13.1 Unit Tests

Create in `GroundUp.Api.Tests.Unit/Settings/`:
- `SettingServiceTests.cs` - Test resolution logic, hierarchy walking
- `SettingValidationServiceTests.cs` - Test all validation rules
- `SettingEncryptionServiceTests.cs` - Test encryption/decryption
- `SettingHierarchyServiceTests.cs` - Test chain building
- `SettingRegistrationServiceTests.cs` - Test code registration

### 13.2 Integration Tests

Create in `GroundUp.Api.Tests.Integration/Settings/`:
- `SettingsCrudIntegrationTests.cs` - Test full CRUD operations
- `SettingsResolutionIntegrationTests.cs` - Test multi-level resolution
- `SettingsEncryptionIntegrationTests.cs` - Test secret storage/retrieval
- `SetupFlowIntegrationTests.cs` - Test bootstrap flow
- `SettingsHierarchyIntegrationTests.cs` - Test custom level registration

### 13.3 Test Scenarios

Key scenarios to cover:
1. Bootstrap and initial setup
2. Setting value resolution with multiple hierarchy levels
3. Override behavior (inherit, explicit, skip level)
4. Secret encryption and decryption
5. Validation (all rule types)
6. Code registration and sync
7. Dynamic hierarchy level registration
8. Encryption migration
9. Conditional settings (showWhen)
10. Permission enforcement

---

## Phase 14: Documentation

### 14.1 Developer Documentation

Create in `docs/`:
- `SETTINGS-OVERVIEW.md` - High-level overview
- `SETTINGS-REGISTRATION.md` - How to register settings in code
- `SETTINGS-HIERARCHY.md` - How hierarchy and resolution works
- `SETTINGS-ENCRYPTION.md` - Encryption and secret management
- `SETTINGS-VALIDATION.md` - Validation rules and custom validators
- `SETTINGS-API.md` - API endpoint reference

### 14.2 User Documentation

Create for end-users:
- How to use the settings UI
- What each core setting means
- How to configure authentication providers
- How to manage secrets
- How hierarchy overrides work

---

## Implementation Phases Summary

### Phase 1 (Foundation): Database & Entities
- Create all database tables and migrations
- Create entity models
- Create DTOs and enums
- **Estimated effort**: 2-3 days

### Phase 2 (Data Access): Repositories
- Implement all repository interfaces and implementations
- Add to DI registration
- Write unit tests
- **Estimated effort**: 2 days

### Phase 3 (Business Logic): Services
- Implement all service interfaces and implementations
- Implement resolution logic with hierarchy walking
- Implement validation service
- Implement encryption service
- Add to DI registration
- Write unit tests
- **Estimated effort**: 4-5 days

### Phase 4 (API): Controllers & Middleware
- Implement all controllers
- Implement setup check middleware
- Add permission decorators
- Write integration tests
- **Estimated effort**: 2-3 days

### Phase 5 (Bootstrap): Installation Flow
- Implement bootstrap settings manager
- Implement installation detection
- Implement setup endpoints
- Seed core settings
- Write integration tests
- **Estimated effort**: 2-3 days

### Phase 6 (Extensibility): Registration System
- Implement code registration API
- Implement registration service
- Implement startup sync
- Test with sample domain settings
- **Estimated effort**: 2 days

### Phase 7 (Advanced): Dynamic Hierarchy & Encryption
- Implement hierarchy registration
- Implement encryption migration
- Test with custom hierarchy levels
- **Estimated effort**: 2-3 days

### Phase 8 (Polish): Validation & UI Schema
- Complete validation system with all rule types
- Implement schema endpoint for UI
- Test dynamic UI scenarios
- **Estimated effort**: 2 days

### Phase 9 (Documentation & Testing)
- Complete all documentation
- Achieve >80% code coverage
- Integration test all scenarios
- **Estimated effort**: 2-3 days

**Total Estimated Effort**: 20-28 days

---

## Success Criteria

? Bootstrap setup flow works end-to-end
? Core settings seeded and accessible
? Setting resolution correctly walks hierarchy
? Secrets properly encrypted/decrypted
? Code registration and sync works
? Dynamic hierarchy levels can be registered
? Validation works for all rule types
? API returns proper schema for UI building
? Permissions properly enforced
? All tests passing (>80% coverage)
? Documentation complete

---

## Future Enhancements

**Post-MVP features to consider**:
1. **Caching layer** with distributed cache support
2. **Real-time updates** with SignalR when settings change
3. **Setting history/audit trail** - track all changes
4. **Setting import/export** - backup/restore configurations
5. **Setting templates** - pre-configured setting bundles
6. **Multi-language support** - localized display names/descriptions
7. **Setting dependencies** - one setting enables/disables others
8. **Bulk operations** - set multiple values at once
9. **Setting search** - full-text search across all settings
10. **Setting comparison** - compare settings across tenants/envs

---

## Open Questions

1. **Setting versioning** - Should we track versions of setting definitions?
2. **Setting deprecation** - How to handle deprecated settings?
3. **Setting migration** - How to handle breaking changes to setting schemas?
4. **Multi-environment** - Different settings for dev/qa/prod?
5. **Setting approval workflow** - Require approval for sensitive setting changes?

---

## Appendix A: Naming Conventions

**Setting Keys**:
- Format: `{source}_{category}_{group}_{setting}`
- Source prefixes:
  - `groundup_` - Core GroundUp settings
  - `{appname}_` - Application-specific settings (e.g., `inventory_`)
  - `user_` - User-created settings
- Always lowercase with underscores
- Examples:
  - `groundup_authentication_keycloak_realm`
  - `inventory_core_general_defaultunit`
  - `user_mycompany_customsetting`

**Hierarchy Level Keys**:
- PascalCase
- Examples: `System`, `Tenant`, `Case`, `Department`, `User`

**Category/Group Display Names**:
- Title Case
- User-friendly
- Examples: "Authentication", "Keycloak Configuration"

---

## Appendix B: Database Migration Script Template

```sql
-- Create SettingCategory table
CREATE TABLE SettingCategory (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    [Key] NVARCHAR(200) NOT NULL UNIQUE,
    DisplayName NVARCHAR(200) NOT NULL,
    Description NVARCHAR(1000),
    Icon NVARCHAR(50),
    SortOrder INT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedBy UNIQUEIDENTIFIER,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ModifiedBy UNIQUEIDENTIFIER,
    ModifiedAt DATETIME2
);

-- Create indexes
CREATE INDEX IX_SettingCategory_Key ON SettingCategory([Key]);
CREATE INDEX IX_SettingCategory_SortOrder ON SettingCategory(SortOrder);

-- ... similar for other tables
```

---

## Appendix C: Example Usage Code

```csharp
// In a service that needs a setting
public class MyService
{
    private readonly ISettingService _settingService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    
    public MyService(ISettingService settingService, IHttpContextAccessor httpContextAccessor)
    {
        _settingService = settingService;
        _httpContextAccessor = httpContextAccessor;
    }
    
    public async Task DoSomethingAsync()
    {
        // Build resolution context from current user/tenant
        var context = new SettingResolutionContext
        {
            UserId = _httpContextAccessor.HttpContext.User.GetUserId(),
            TenantId = _httpContextAccessor.HttpContext.User.GetTenantId()
        };
        
        // Get resolved value (walks hierarchy automatically)
        var maxUploadSize = await _settingService.GetResolvedValueAsync<int>(
            "groundup_upload_maxsize", 
            context);
        
        // Use the value
        if (fileSize > maxUploadSize)
            throw new Exception("File too large");
    }
}
```

---

## Next Steps

1. **Review and approve this plan**
2. **Create GitHub issues** for each phase
3. **Set up project board** with phases as columns
4. **Begin Phase 1** (Database & Entities)
5. **Schedule daily check-ins** during implementation

**Let's build this!** ??
