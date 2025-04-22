﻿using Newtonsoft.Json;

namespace Helios.Classes.MCP;

public class LoadoutDefinition
{
    public bool? item_seen { get; set; }
    public List<Variants>? variants { get; set; }
    public int? xp { get; set; }
    public int? use_count { get; set; }
    public int? level { get; set; }
    public bool? favorite { get; set; }
    public string? platform { get; set; }
    public string? banner_color_template { get; set; }
    public string? banner_icon_template { get; set; }
    public string? locker_name { get; set; }
    public LockerSlotData? locker_slots_data { get; set; }
}