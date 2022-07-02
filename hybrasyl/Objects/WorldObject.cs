﻿/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2020 ERISCO, LLC 
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */


using System;
using System.Collections.Generic;
using System.Drawing;
using C3;
using Hybrasyl.Dialogs;
using Hybrasyl.Interfaces;
using Hybrasyl.Scripting;
using Newtonsoft.Json;

namespace Hybrasyl.Objects;

[JsonObject(MemberSerialization.OptIn)]
public class WorldObject : IQuadStorable, IWorldObject
{
    /// <summary>
    /// The rectangle that defines the object's boundaries.
    /// </summary>
    public Rectangle Rect => new(X, Y, 1, 1);

    public DateTime CreationTime { get; set; }

    public bool HasMoved { get; set; }
    public string Type => GetType().Name;

    public virtual byte X { get; set; }
    public virtual byte Y { get; set; }
    public uint Id { get; set; }
    [JsonProperty(Order = 0)] public Guid Guid { get; set; } = Guid.NewGuid();

    [JsonProperty(Order = 0)]
    public virtual string Name { get; set; }

    public virtual Script Script { get; set; }

    public ScriptExecutionResult LastExecutionResult { get; set; }

    public Guid ServerGuid { get; set; }
    public World World => Game.GetServerByGuid<World>(ServerGuid);
    public ushort DialogSprite { get; set; }


    public virtual void OnInsert() { }


    public WorldObject()
    {
        Name = string.Empty;
        CreationTime = DateTime.Now;
    }

    public virtual void SendId()
    {
    }

    public virtual int Distance(WorldObject obj)
    {
        if (obj == null) return 255;
        return Point.Distance(obj.X, obj.Y, X, Y);
    }

} 