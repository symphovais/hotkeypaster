// Re-export SDK types for internal use
// External plugins should reference TalkKeys.PluginSdk directly

global using TalkKeys.PluginSdk;
global using ITriggerPlugin = TalkKeys.PluginSdk.ITriggerPlugin;
global using TriggerEventArgs = TalkKeys.PluginSdk.TriggerEventArgs;
global using TriggerConfiguration = TalkKeys.PluginSdk.TriggerConfiguration;
global using TriggerPluginConfiguration = TalkKeys.PluginSdk.TriggerPluginConfiguration;
global using TriggerInfo = TalkKeys.PluginSdk.TriggerInfo;
global using RecordingTriggerAction = TalkKeys.PluginSdk.RecordingTriggerAction;

namespace TalkKeys.Services.Triggers
{
    // This file re-exports SDK types for use within the main application.
    // The actual interface definitions are in TalkKeys.PluginSdk.
}
