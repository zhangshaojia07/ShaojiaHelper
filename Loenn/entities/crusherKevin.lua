local drawableNinePatch = require("structs.drawable_nine_patch")
local drawableRectangle = require("structs.drawable_rectangle")
local drawableSprite = require("structs.drawable_sprite")

local crusherKevin = {}

crusherKevin.name = "ShaojiaHelper/CrusherKevin"
crusherKevin.depth = 0
crusherKevin.minimumSize = {24, 24}
crusherKevin.fieldInformation = {} -- Boolean fields become checkboxes in Loenn automatically; no extra configuration needed

-- Single entity placement configuration
crusherKevin.placements = {
    {
        name = "Crusher Kevin",
        data = {
            width = 24,
            height = 24,
            lookUp = true,
            lookDown = true,
            lookLeft = true,
            lookRight = true,
            chillout = false,
            requiresLineOfSight = true,
            interruptable = false,
            nonReturn = true
        }
    }
}

local ninePatchOptions = {
    mode = "border",
    borderMode = "repeat"
}

local crusherKevinColor = {98 / 255, 34 / 255, 43 / 255}
local smallFaceTexture = "objects/crushblock/idle_face"
local giantFaceTexture = "objects/crushblock/giant_block00"

function crusherKevin.sprite(room, entity)
    local x, y = entity.x or 0, entity.y or 0
    local width, height = entity.width or 24, entity.height or 24

    local up = entity.lookUp or false
    local down = entity.lookDown or false
    local left = entity.lookLeft or false
    local right = entity.lookRight or false
    local chillout = entity.chillout

    -- 1. Build a bitmask out of the four directions
    -- If the weights in the C# code differ (e.g. Left = 1, Right = 2, ...), just swap the numbers below
    local mask = 0
    if up then mask = mask + 2 end
    if down then mask = mask + 8 end
    if left then mask = mask + 4 end
    if right then mask = mask + 1 end

    -- 2. Turn the mask into a zero-padded two-digit string (e.g. "00", "01", "05", "15")
    local maskString = string.format("%02d", mask)

    -- 3. Build the texture path from that string, so it matches block00, block01 ... block15
    local frameTexture = "objects/ShaojiaHelper/crusherKevin/block" .. maskString

    local giant = height >= 48 and width >= 48 and chillout
    local faceTexture = giant and giantFaceTexture or smallFaceTexture

    local ninePatch = drawableNinePatch.fromTexture(frameTexture, ninePatchOptions, x, y, width, height)

    local rectangle = drawableRectangle.fromRectangle("fill", x + 2, y + 2, width - 4, height - 4, crusherKevinColor)
    local faceSprite = drawableSprite.fromTexture(faceTexture, entity)

    faceSprite:addPosition(math.floor(width / 2), math.floor(height / 2))

    local sprites = ninePatch:getDrawableSprite()

    table.insert(sprites, 1, rectangle:getDrawableSprite())
    table.insert(sprites, 2, faceSprite)

    return sprites
end

return crusherKevin