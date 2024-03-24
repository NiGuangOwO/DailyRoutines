using System.Collections.Generic;
using ClickLib.Clicks;
using DailyRoutines.Infos;
using DailyRoutines.Managers;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using ImGuiNET;

namespace DailyRoutines.Modules;

[ModuleDescription("AutoRequestItemSubmitTitle", "AutoRequestItemSubmitDescription", ModuleCategories.Base)]
public class AutoRequestItemSubmit : DailyModuleBase
{
    private static bool ConfigIsSubmitHQItem;

    private static readonly List<int> SlotsFilled = [];

    public override void Init()
    {
        AddConfig(this, "IsSubmitHQItem", true);

        ConfigIsSubmitHQItem = GetConfig<bool>(this, "IsSubmitHQItem");

        Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "Request", OnAddonRequest);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PostDraw, "Request", OnAddonRequest);
        Service.AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, "Request", OnAddonRequest);
        TaskManager ??= new TaskManager { AbortOnTimeout = true, TimeLimitMS = 5000, ShowDebug = false };
    }

    public override void ConfigUI()
    {
        ConflictKeyText();
        if (ImGui.Checkbox("递交优质道具", ref ConfigIsSubmitHQItem))
            UpdateConfig(this, "IsSubmitHQItem", ConfigIsSubmitHQItem);
    }
    private void OnAddonRequest(AddonEvent type, AddonArgs args)
    {
        if (Service.KeyState[Service.Config.ConflictKey])
        {
            AbortActions();
            return;
        }

        switch (type)
        {
            case AddonEvent.PostSetup:
                Service.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", OnAddonSelectYesno);
                break;
            case AddonEvent.PostDraw:
                ClickRequestIcon();
                break;
            case AddonEvent.PreFinalize:
                AbortActions();
                break;
        }
    }

    private void AbortActions()
    {
        SlotsFilled.Clear();
        TaskManager?.Abort();
        Service.AddonLifecycle.UnregisterListener(OnAddonSelectYesno);
    }

    private static unsafe void OnAddonSelectYesno(AddonEvent type, AddonArgs args)
    {
        if (!ConfigIsSubmitHQItem) return;
        if (TryGetAddonByName<AddonSelectYesno>("SelectYesno", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var text = addon->PromptText->NodeText.ExtractText();
            if (text != null && text.Contains("优质"))
            {
                var handler = new ClickSelectYesNo();
                handler.Yes();
            }
        }
    }

    private unsafe void ClickRequestIcon()
    {
        if (TryGetAddonByName<AddonRequest>("Request", out var addon))
        {
            for (var i = 1; i <= addon->EntryCount; i++)
            {
                if (SlotsFilled.Contains(addon->EntryCount)) ClickHandOver();
                if (SlotsFilled.Contains(i)) return;
                var index = i;
                TaskManager.DelayNext($"AutoRequestItemSubmit{index}", 10);
                TaskManager.Enqueue(() => TryClickItem(addon, index));
            }
        }
        else
        {
            SlotsFilled.Clear();
            TaskManager.Abort();
        }
    }

    private static unsafe bool? TryClickItem(AddonRequest* addon, int i)
    {
        if (SlotsFilled.Contains(i)) return true;

        var contextMenu = (AtkUnitBase*)Service.Gui.GetAddonByName("ContextIconMenu");

        if (contextMenu is null || !contextMenu->IsVisible)
        {
            var slot = i - 1;

            AddonManager.Callback(&addon->AtkUnitBase, false, 2, slot, 0, 0);

            return false;
        }

        AddonManager.Callback(contextMenu, false, 0, 0, 1021003, 0, 0);
        SlotsFilled.Add(i);
        return true;
    }

    private static unsafe void ClickHandOver()
    {
        if (TryGetAddonByName<AddonRequest>("Request", out var addon) &&
            HelpersOm.IsAddonAndNodesReady(&addon->AtkUnitBase))
        {
            var handler = new ClickRequest();
            handler.HandOver();
        }
    }

    public override void Uninit()
    {
        Service.AddonLifecycle.UnregisterListener(OnAddonRequest);
        AbortActions();

        base.Uninit();
    }
}
