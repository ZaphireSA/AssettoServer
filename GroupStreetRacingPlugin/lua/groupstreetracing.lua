local baseUrl = "http://" .. ac.getServerIP() .. ":" .. ac.getServerPortHTTP() .. "/groupstreetracing/"

local StreetRaceStatus = {
    Challenging = 0,
    Starting = 1,
    Started = 2,
    Ended = 3,
    Cancelled = 4
}

local EventType = {
    None = 0,
    RacePlayerJoined = 1,
    RacePlayerLeft = 2,
    RaceEnded = 3,
    PlayerCrashed = 4,
    PlayerLeftBehind = 5,
    CountdownStart = 6
}

local ownSessionId = ac.getCar(0).sessionID

local carsWithHazards = {}
local carsInRace = {}
local packetLen = 20

local street_race_status = {
    CHALLENGING = 1,
    STARTING = 2,
    IN_PROGRESS = 3,
    ENDED = 4,
    CANCELLED = 5,
    COUNTDOWN = 6
}

local racer_status = {
    NONE = 0,
    NOT_READY = 1,
    READY = 2,
    RACING = 3,
    ELIMINATED = 4,
    CRASHED = 5,
    DISCONNECTED = 6,
    BACK_TO_PIT = 7
}

local raceStatus;
local raceStartTime = -1

local currentRaceEvent = ac.OnlineEvent({
    ac.StructItem.key("groupStreetRacingCurrentRacePacket"),
    SessionIds = ac.StructItem.array(ac.StructItem.byte(), packetLen),
    HealthOfCars = ac.StructItem.array(ac.StructItem.byte(), packetLen),
    RacersStatus = ac.StructItem.array(ac.StructItem.byte(), packetLen),
    RaceStatus = ac.StructItem.byte()
}, function(sender, data)
    -- only accept packets from server
    if sender ~= nil then
        return
    end
    carsInRace = {}
    for i = 0, packetLen - 1 do

        local sessionId = data.SessionIds[i]
        if sessionId ~= 255 then
            carsInRace[i] = {
                SessionId = sessionId,
                PositionInRace = i + 1,
                Health = data.HealthOfCars[i],
                Status = data.RacersStatus[i]
            }
        else
            carsInRace[i] = {
                SessionId = 255,
                PositionInRace = -1,
                Health = -1,
                Status = racer_status.NONE
            }
        end
    end
    raceStatus = data.RaceStatus
    ac.debug("raceStatus", data.RaceStatus)
    ac.debug("car1SessionId", carsInRace[0].SessionId)
    ac.debug("car1PositionInRace", carsInRace[0].PositionInRace)
    ac.debug("car1Health", carsInRace[0].Health)
    ac.debug("car1Status", carsInRace[0].Status)
end)

local lastEventType;
local lastEventData;

local raceUpdateEvent = ac.OnlineEvent({
    ac.StructItem.key("groupStreetRacingUpdateEventPacket"),
    EventType = ac.StructItem.byte(),
    EventData = ac.StructItem.int32(),
}, function(sender, data)
    -- only accept packets from server
    if sender ~= nil then
        return
    end

    lastEventType = data.EventType
    lastEventData = data.EventData

    if data.EventType == EventType.CountdownStart then
        raceStartTime = data.EventData
    end

    if data.EventType == EventType.PlayerCrashed then
        ui.toast(ui.Icons.WeatherClear, GetDriverNameBySessionId(data.EventData) .. " has crashed out of the race.")
    end

    if data.EventType == EventType.PlayerLeftBehind then
        ui.toast(ui.Icons.WeatherClear, GetDriverNameBySessionId(data.EventData) .. " has been elimenated from the race.")
    end

    if data.EventType == EventType.RaceEnded then
        ui.toast(ui.Icons.WeatherClear, "The race has ended.")
    end

    ac.debug("eventType", data.EventType)
    ac.debug("eventData", data.EventData)
end)

--   function GetOwnRanking(callback)
--     web.get(leaderboardUrl .. "/" .. ac.getUserSteamID(), function (err, response)
--       callback(stringify.parse(response.body))
--     end)
--   end

function GetDriverNameBySessionId(sessionId)
    local count = ac.getSim().carsCount
    for i = 0, count do
        local car = ac.getCar(i)
        if car.sessionID == sessionId then
            return ac.getDriverName(car.index)
        end
    end
end

function script.drawUI()
    local currentTime = GetSessionTime()
    local raceTimeElapsed = currentTime - raceStartTime
    if lastEventType == EventType.CountdownStart then
        if raceTimeElapsed > -6000 and raceTimeElapsed < 0 then
            local text = math.ceil(raceTimeElapsed / 1000 * -1)
            DrawTextCentered(text)
        elseif raceTimeElapsed > 0 then
            if raceTimeElapsed < 1000 then
                DrawTextCentered("Go!")
            end
        end
    end
    -- DrawTextCentered("You lost the race.")
    RacePartyHUD()
end

function GetSessionTime()
    return ac.getSim().timeToSessionStart * -1
end

function DrawTextCentered(text)
    local uiState = ac.getUI()

    ui.transparentWindow('raceText', vec2(uiState.windowSize.x / 2 - 250, uiState.windowSize.y / 2 - 250), vec2(500, 100)
        ,
        function()
            ui.pushFont(ui.Font.Huge)

            local size = ui.measureText(text)
            ui.setCursorX(ui.getCursorX() + ui.availableSpaceX() / 2 - (size.x / 2))
            ui.text(text)

            ui.popFont()
        end)
end

function PrintCarWithHazardsRow(name, position, health, status)
    ui.text(tostring(name))
    ui.nextColumn()
    ui.text(tostring(position))
    ui.nextColumn()
    -- ui.text(tostring(health))
    local barSize = vec2(ui.availableSpaceX(), 15)
    local barColor = rgbm(1, 1, 1, 1)
    local progress = (health + .0) / 100
    barColor:setLerp(rgbm.colors.red, rgbm.colors.white, progress)
    ui.drawRect(ui.getCursor(), ui.getCursor() + barSize, barColor);
    local p1, p2
    p1 = ui.getCursor()
    p2 = ui.getCursor() + vec2(barSize.x * progress, barSize.y)

    ui.drawRectFilled(p1, p2, barColor)
    ui.dummy(barSize)

    ui.nextColumn()

    if status == racer_status.READY then
        ui.text("Ready")
        ui.nextColumn()
    elseif status == racer_status.RACING then
        ui.text("Racing")
        ui.nextColumn()
    elseif status == racer_status.CRASHED then
        ui.text("Crashed")
        ui.nextColumn()
    elseif status == racer_status.ELIMINATED then
        ui.text("Eliminated")
        ui.nextColumn()
    elseif status == racer_status.BACK_TO_PIT then
        ui.text("Back to pit")
        ui.nextColumn()
    elseif status == racer_status.DISCONNECTED then
        ui.text("Disconnected")
        ui.nextColumn()
    else
        ui.text(tostring(status))
        ui.nextColumn()
    end


end

function PrintCarWithHazardsRowHeader(name, position, health, status)
    ui.text(tostring(name))
    ui.nextColumn()
    ui.text(tostring(position))
    ui.nextColumn()
    ui.text(tostring(health))
    ui.nextColumn()
    ui.text(tostring(status))
    ui.nextColumn()
end

function RacePartyHUD()
    ui.childWindow('groupStreetRacingList', vec2(0, 275), true, ui.WindowFlags.None, function()
        if #carsInRace == 0 then
            ui.text("No cars in race yet")
            ac.debug("nocars", "yes")
        else
            ui.columns(4)
            ui.setColumnWidth(0, 200)
            ui.setColumnWidth(1, 200)
            ui.setColumnWidth(2, 200)
            ui.setColumnWidth(3, 200)

            PrintCarWithHazardsRowHeader("Racer", "Pos", "Health", "Status")

            for i, carsInRace in pairs(carsInRace) do
                if carsInRace.SessionId ~= 255 then
                    PrintCarWithHazardsRow(GetDriverNameBySessionId(carsInRace.SessionId), carsInRace.PositionInRace,
                        carsInRace.Health, carsInRace.Status)
                end
            end

            ui.columns()
            ac.debug("columns", "yes")
        end
        ui.offsetCursorY(ui.availableSpaceY() - 32)
        if ui.button("Close") then
            close = true
        end

        ac.debug("hasLoadedUI", "yes")
    end)
end

function RaceHudClosed()
end

ui.registerOnlineExtra(ui.Icons.LightThunderstorm, 'Cars With Hazards', nil, RacePartyHUD, RaceHudClosed,
    ui.OnlineExtraFlags.Tool)
-- function ui.registerOnlineExtra(iconID, title, availableCallback, uiCallback, closeCallback, flags) end

-- ui.registerOnlineExtra(ui.Icons.Leaderboard, "Cars With Hazards", function() return true end, function()
--     local close = false
--     ui.childWindow('groupStreetRacingList', vec2(0, 275), false, ui.WindowFlags.None, function()
--         if carsWithHazards == nil then
--             ui.text("No cars with hazards yet")
--             ac.debug("nocars", "yes")
--         else
--             ui.columns(2)
--             ui.setColumnWidth(0, 45)
--             ui.setColumnWidth(1, 200)

--             PrintCarWithHazardsRow("#", "Distance")

--             for i, sessionId in ipairs(carsWithHazards) do
--                 PrintCarWithHazardsRow(GetDriverNameBySessionId(sessionId), 0)
--             end

--             ui.columns()
--             ac.debug("columns", "yes")
--         end
--         ui.offsetCursorY(ui.availableSpaceY() - 32)
--         if ui.button("Close") then
--             close = true
--         end

--         ac.debug("hasLoadedUI", "yes")
--     end)

--     return close
-- end)
