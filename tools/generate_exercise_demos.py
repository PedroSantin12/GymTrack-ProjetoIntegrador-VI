from __future__ import annotations

import math
from pathlib import Path

from PIL import Image, ImageDraw

WIDTH = 360
HEIGHT = 240
SCALE = 2
FRAMES = 28
DURATION = 110
ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "GymTrack" / "Resources" / "Images"

BG = "#F8F9F8"
FLOOR = "#D8DADC"
BODY = "#D8DADC"
BODY_LIGHT = "#EEF0F0"
LINE = "#71777A"
DETAIL = "#A7ACAE"
MUSCLE = "#F36F43"
MUSCLE_DARK = "#B8472B"
EQUIPMENT = "#2D3234"


class Canvas:
    def __init__(self):
        self.image = Image.new("RGB", (WIDTH * SCALE, HEIGHT * SCALE), BG)
        self.draw = ImageDraw.Draw(self.image)
        self.line([(16, 225), (344, 225)], 1, FLOOR)

    @staticmethod
    def xy(point):
        return tuple(round(value * SCALE) for value in point)

    def line(self, points, width, color, outline=None):
        pts = [self.xy(point) for point in points]
        if outline:
            self.draw.line(pts, fill=outline, width=round((width + 3) * SCALE), joint="curve")
        self.draw.line(pts, fill=color, width=round(width * SCALE), joint="curve")
        radius = round(width * SCALE / 2)
        for x, y in (pts[0], pts[-1]):
            self.draw.ellipse((x - radius, y - radius, x + radius, y + radius), fill=color)
            if outline:
                self.draw.ellipse((x - radius, y - radius, x + radius, y + radius), outline=outline, width=SCALE)

    def oval(self, box, fill, outline=None, width=1):
        x0, y0, x1, y1 = [round(value * SCALE) for value in box]
        self.draw.ellipse((x0, y0, x1, y1), fill=fill, outline=outline, width=width * SCALE)

    def rect(self, box, fill, outline=None, width=1, radius=0):
        x0, y0, x1, y1 = [round(value * SCALE) for value in box]
        self.draw.rounded_rectangle(
            (x0, y0, x1, y1), radius=radius * SCALE, fill=fill,
            outline=outline, width=width * SCALE
        )

    def poly(self, points, fill, outline=None):
        self.draw.polygon([self.xy(point) for point in points], fill=fill)
        if outline:
            self.draw.line([self.xy(point) for point in points + [points[0]]],
                           fill=outline, width=SCALE, joint="curve")

    def dumbbell(self, center, vertical=False):
        x, y = center
        if vertical:
            self.line([(x, y - 12), (x, y + 12)], 3, EQUIPMENT)
            self.rect((x - 6, y - 15, x + 6, y - 9), EQUIPMENT, radius=2)
            self.rect((x - 6, y + 9, x + 6, y + 15), EQUIPMENT, radius=2)
        else:
            self.line([(x - 11, y), (x + 11, y)], 3, EQUIPMENT)
            self.rect((x - 14, y - 6, x - 9, y + 6), EQUIPMENT, radius=2)
            self.rect((x + 9, y - 6, x + 14, y + 6), EQUIPMENT, radius=2)

    def head(self, center, tilt=0):
        x, y = center
        self.oval((x - 14 + tilt, y - 18, x + 14 + tilt, y + 18), BODY_LIGHT, LINE)
        self.line([(x - 5 + tilt, y + 2), (x - 1 + tilt, y + 2)], 1, DETAIL)
        self.line([(x + 3 + tilt, y + 2), (x + 7 + tilt, y + 2)], 1, DETAIL)

    def finish(self):
        return self.image.resize((WIDTH, HEIGHT), Image.Resampling.LANCZOS)


def standing(mode, phase, focus):
    c = Canvas()
    p = phase
    lower = mode in {"squat", "sumo"}
    shift = 28 * p if lower else -10 * p if mode in {"calf", "shrug"} else 0
    shoulder_y = 67 + shift
    hip_y = 142 + shift
    spread = p if mode == "abductor" else 1 - p if mode == "adductor" else 0
    knee_x = 152 - (30 * p if lower else 30 * spread)
    knee_rx = WIDTH - knee_x
    ankle_y = 216 - (10 * p if mode == "calf" else 0)
    ankle_l = 152 - 38 * spread if mode != "sumo" else 133
    ankle_r = WIDTH - ankle_l
    if mode == "jack":
        ankle_l -= 36 * p
        ankle_r += 36 * p

    c.line([(162, hip_y), (knee_x, 183 + (10 * p if lower else 0)),
            (ankle_l, ankle_y)], 19, MUSCLE if focus in {"quads", "hamstrings"} else BODY, LINE)
    c.line([(198, hip_y), (knee_rx, 183 + (10 * p if lower else 0)),
            (ankle_r, ankle_y)], 19, MUSCLE if focus in {"quads", "hamstrings"} else BODY, LINE)
    if focus == "calves":
        c.line([(knee_x, 183), (ankle_l, ankle_y)], 13, MUSCLE, MUSCLE_DARK)
        c.line([(knee_rx, 183), (ankle_r, ankle_y)], 13, MUSCLE, MUSCLE_DARK)
    c.oval((ankle_l - 14, ankle_y - 3, ankle_l + 10, ankle_y + 7), BODY_LIGHT, LINE)
    c.oval((ankle_r - 9, ankle_y - 3, ankle_r + 15, ankle_y + 7), BODY_LIGHT, LINE)
    c.oval((157, hip_y - 12, 203, hip_y + 13), MUSCLE if focus == "glutes" else BODY, LINE)

    c.poly([(151, shoulder_y), (209, shoulder_y), (204, shoulder_y + 40),
            (198, hip_y - 5), (162, hip_y - 5), (156, shoulder_y + 40)], BODY_LIGHT, LINE)
    if focus == "chest":
        c.oval((154, shoulder_y + 6, 179, shoulder_y + 29), MUSCLE, MUSCLE_DARK)
        c.oval((181, shoulder_y + 6, 206, shoulder_y + 29), MUSCLE, MUSCLE_DARK)
    else:
        c.oval((154, shoulder_y + 7, 179, shoulder_y + 26), BODY, DETAIL)
        c.oval((181, shoulder_y + 7, 206, shoulder_y + 26), BODY, DETAIL)
    for y in (shoulder_y + 39, shoulder_y + 51, shoulder_y + 63):
        if y < hip_y - 6:
            c.line([(168, y), (192, y)], 1, MUSCLE_DARK if focus == "abs" else DETAIL)
    if focus == "abs":
        c.rect((170, shoulder_y + 34, 190, hip_y - 12), MUSCLE, radius=6)
    c.line([(179, shoulder_y + 32), (179, hip_y - 7)], 1, DETAIL)
    if focus == "back":
        c.poly([(150, shoulder_y + 1), (175, shoulder_y + 12),
                (168, shoulder_y + 36), (147, shoulder_y + 20)], MUSCLE, MUSCLE_DARK)
        c.poly([(210, shoulder_y + 1), (185, shoulder_y + 12),
                (192, shoulder_y + 36), (213, shoulder_y + 20)], MUSCLE, MUSCLE_DARK)
    if focus == "traps":
        c.oval((145, shoulder_y - 7, 168, shoulder_y + 15), MUSCLE, MUSCLE_DARK)
        c.oval((192, shoulder_y - 7, 215, shoulder_y + 15), MUSCLE, MUSCLE_DARK)

    neck_y = shoulder_y - 20
    c.line([(180, shoulder_y), (180, neck_y + 8)], 12, BODY, LINE)
    c.head((180, neck_y - 8), tilt=(10 * math.sin(2 * math.pi * p) if mode == "neck" else 0))

    if mode in {"lateral", "jack"}:
        extension = p
        elbow_l = (143 - 31 * extension, shoulder_y + 42 - 30 * extension)
        hand_l = (145 - 58 * extension, shoulder_y + 70 - 68 * extension)
        elbow_r = (360 - elbow_l[0], elbow_l[1])
        hand_r = (360 - hand_l[0], hand_l[1])
    elif mode in {"curl", "wrist"}:
        elbow_l, elbow_r = (142, shoulder_y + 42), (218, shoulder_y + 42)
        amount = 57 * p if mode == "curl" else 28 * p
        hand_l, hand_r = (143, shoulder_y + 76 - amount), (217, shoulder_y + 76 - amount)
    elif mode == "press":
        elbow_l, elbow_r = (136 + 8 * p, shoulder_y + 30 - 33 * p), (224 - 8 * p, shoulder_y + 30 - 33 * p)
        hand_l, hand_r = (135 + 10 * p, shoulder_y + 10 - 55 * p), (225 - 10 * p, shoulder_y + 10 - 55 * p)
    elif mode == "upright":
        elbow_l, elbow_r = (130 - 7 * p, shoulder_y + 39 - 37 * p), (230 + 7 * p, shoulder_y + 39 - 37 * p)
        hand_l, hand_r = (160, shoulder_y + 75 - 65 * p), (200, shoulder_y + 75 - 65 * p)
    elif mode == "pulldown":
        elbow_l, elbow_r = (125 + 7 * p, shoulder_y + 7 + 26 * p), (235 - 7 * p, shoulder_y + 7 + 26 * p)
        hand_l, hand_r = (128, shoulder_y - 56 + 55 * p), (232, shoulder_y - 56 + 55 * p)
    elif mode == "triceps":
        elbow_l, elbow_r = (148, shoulder_y + 30), (212, shoulder_y + 30)
        hand_l, hand_r = (157, shoulder_y + 21 + 42 * p), (203, shoulder_y + 21 + 42 * p)
    elif mode == "overhead_triceps":
        elbow_l, elbow_r = (166, shoulder_y - 18), (194, shoulder_y - 18)
        hand_l, hand_r = (174, shoulder_y - 53 + 39 * p), (186, shoulder_y - 53 + 39 * p)
    elif mode == "stretch":
        elbow_l, elbow_r = (133 - 10 * p, shoulder_y + 38 - 65 * p), (227 + 10 * p, shoulder_y + 38 - 65 * p)
        hand_l, hand_r = (138 - 15 * p, shoulder_y + 76 - 118 * p), (222 + 15 * p, shoulder_y + 76 - 118 * p)
    elif mode == "shrug":
        elbow_l, elbow_r = (138, shoulder_y + 38), (222, shoulder_y + 38)
        hand_l, hand_r = (139, shoulder_y + 79), (221, shoulder_y + 79)
    else:
        elbow_l, elbow_r = (139, shoulder_y + 38), (221, shoulder_y + 38)
        hand_l, hand_r = (144, shoulder_y + 78), (216, shoulder_y + 78)

    arm_color = MUSCLE if focus in {"biceps", "triceps", "forearms"} else BODY
    c.line([(150, shoulder_y), elbow_l], 15, arm_color if focus == "biceps" else BODY, LINE)
    c.line([elbow_l, hand_l], 12, arm_color if focus in {"biceps", "triceps", "forearms"} else BODY, LINE)
    c.line([(210, shoulder_y), elbow_r], 15, arm_color if focus == "biceps" else BODY, LINE)
    c.line([elbow_r, hand_r], 12, arm_color if focus in {"biceps", "triceps", "forearms"} else BODY, LINE)
    if focus == "shoulders":
        c.oval((140, shoulder_y - 8, 159, shoulder_y + 18), MUSCLE, MUSCLE_DARK)
        c.oval((201, shoulder_y - 8, 220, shoulder_y + 18), MUSCLE, MUSCLE_DARK)

    if mode in {"lateral", "curl", "wrist", "press", "shrug", "upright"}:
        c.dumbbell(hand_l)
        c.dumbbell(hand_r)
    if mode == "pulldown":
        c.line([(125, shoulder_y - 57), (235, shoulder_y - 57)], 5, EQUIPMENT)
        c.line([(180, 7), (180, shoulder_y - 57)], 2, EQUIPMENT)
    if mode == "triceps":
        c.line([(180, 8), (180, hand_l[1] - 6)], 2, EQUIPMENT)
        c.line([(155, hand_l[1]), (205, hand_r[1])], 5, EQUIPMENT)
    if mode == "overhead_triceps":
        c.dumbbell((180, hand_l[1]), vertical=True)
    return c.finish()


def bench(mode, p, focus):
    c = Canvas()
    incline = mode == "incline"
    c.rect((87, 148, 263, 158), EQUIPMENT, radius=4)
    c.line([(106, 156), (101, 216)], 6, EQUIPMENT)
    c.line([(246, 156), (255, 216)], 6, EQUIPMENT)
    if incline:
        c.line([(98, 150), (150, 90)], 10, EQUIPMENT)
        neck = (137, 83)
        shoulder = (150, 98)
        hip = (205, 147)
    else:
        neck = (98, 123)
        shoulder = (121, 130)
        hip = (213, 136)
    c.line([(hip[0], hip[1]), (245, 162), (259, 198)], 21, BODY, LINE)
    c.line([(hip[0] + 4, hip[1]), (256, 158), (276, 201)], 18, BODY, LINE)
    c.oval((247, 193, 279, 207), BODY_LIGHT, LINE)
    c.line([neck, shoulder, hip], 31, BODY_LIGHT, LINE)
    c.oval((shoulder[0] + 5, shoulder[1] - 15,
            shoulder[0] + 54, shoulder[1] + 17), MUSCLE if focus == "chest" else BODY, MUSCLE_DARK if focus == "chest" else DETAIL)
    c.head((neck[0] - 21, neck[1] - 3))
    if mode == "fly":
        hand_a = (shoulder[0] + 52 + 26 * (1 - p), shoulder[1] - 56 + 34 * (1 - p))
        hand_b = (shoulder[0] + 13 - 19 * (1 - p), shoulder[1] - 58 + 34 * (1 - p))
        elbow_a = ((shoulder[0] + hand_a[0]) / 2, (shoulder[1] + hand_a[1]) / 2)
        elbow_b = ((shoulder[0] + hand_b[0]) / 2, (shoulder[1] + hand_b[1]) / 2)
    else:
        hand_a = (shoulder[0] + 23, shoulder[1] - 19 - 65 * p)
        hand_b = (shoulder[0] + 55, shoulder[1] - 22 - 65 * p)
        elbow_a = (shoulder[0] + 33, shoulder[1] + 5 - 33 * p)
        elbow_b = (shoulder[0] + 62, shoulder[1] + 7 - 33 * p)
    c.line([shoulder, elbow_a, hand_a], 13, BODY, LINE)
    c.line([(shoulder[0] + 28, shoulder[1] + 4), elbow_b, hand_b], 11, BODY, LINE)
    c.dumbbell(hand_a)
    c.dumbbell(hand_b)
    return c.finish()


def horizontal(mode, p, focus):
    c = Canvas()
    if mode in {"plank", "pushup"}:
        y = 123 + (29 * p if mode == "pushup" else 2 * p)
        c.line([(80, y - 21), (123, y), (215, y + 5), (281, 172)], 23, BODY_LIGHT, LINE)
        c.head((65, y - 25))
        c.line([(124, y), (133, 182 - 6 * p), (150, 191)], 13, BODY, LINE)
        c.line([(216, y + 5), (274, 180), (300, 187)], 16, BODY, LINE)
        c.oval((119, y - 15, 171, y + 13), MUSCLE if focus == "chest" else BODY, MUSCLE_DARK if focus == "chest" else DETAIL)
        c.oval((200, y - 7, 229, y + 15), MUSCLE if focus == "abs" else BODY, MUSCLE_DARK if focus == "abs" else DETAIL)
        c.rect((126, 187, 168, 195), EQUIPMENT, radius=3)
        return c.finish()
    if mode == "crunch":
        c.rect((73, 178, 279, 184), EQUIPMENT, radius=2)
        c.line([(186, 164), (228, 142), (250, 178)], 20, BODY, LINE)
        c.line([(190, 164), (244, 149), (267, 178)], 16, BODY, LINE)
        c.oval((172, 146, 206, 177), BODY, LINE)
        c.line([(185, 157), (137, 154 - 17 * p), (108 - 8 * p, 122 - 38 * p)], 28, BODY_LIGHT, LINE)
        c.oval((145, 133 - 13 * p, 178, 165 - 9 * p), MUSCLE, MUSCLE_DARK)
        c.head((91 - 8 * p, 116 - 38 * p))
        c.line([(132, 144 - 13 * p), (112, 121 - 31 * p)], 12, BODY, LINE)
        return c.finish()
    if mode == "hip_thrust":
        c.rect((52, 154, 141, 164), EQUIPMENT, radius=4)
        c.line([(75, 162), (76, 217)], 6, EQUIPMENT)
        hip = (190, 168 - 42 * p)
        c.line([(111, 148), hip, (247, 173)], 25, BODY_LIGHT, LINE)
        c.head((91, 138))
        c.oval((176, hip[1] - 14, 205, hip[1] + 16), MUSCLE, MUSCLE_DARK)
        c.line([(247, 173), (265, 211)], 19, BODY, LINE)
        c.oval((251, 207, 283, 217), BODY_LIGHT, LINE)
        c.line([(137, 146), (176, 149)], 10, BODY, LINE)
        return c.finish()
    if mode == "kickback":
        c.rect((84, 188, 288, 196), EQUIPMENT, radius=3)
        c.line([(140, 98), (111, 154), (112, 188)], 19, BODY, LINE)
        c.line([(139, 102), (216, 121)], 30, BODY_LIGHT, LINE)
        c.head((126, 77))
        c.oval((201, 105, 232, 136), MUSCLE, MUSCLE_DARK)
        c.line([(213, 119), (198, 165), (207, 186)], 19, BODY, LINE)
        c.line([(217, 121), (262, 151 - 29 * p), (298, 171 - 57 * p)], 17, BODY, LINE)
        return c.finish()
    if mode == "leg_curl":
        c.rect((76, 141, 261, 151), EQUIPMENT, radius=4)
        c.line([(110, 150), (108, 214)], 6, EQUIPMENT)
        c.line([(249, 150), (248, 214)], 6, EQUIPMENT)
        c.head((88, 109))
        c.line([(115, 128), (209, 128)], 29, BODY_LIGHT, LINE)
        c.line([(207, 129), (251, 126)], 22, MUSCLE, MUSCLE_DARK)
        c.line([(251, 126), (281 - 19 * p, 156 - 69 * p)], 17, BODY, LINE)
        c.line([(246, 130), (265 - 18 * p, 160 - 68 * p)], 14, BODY, LINE)
        c.rect((274 - 20 * p, 150 - 69 * p, 291 - 20 * p, 168 - 69 * p), EQUIPMENT, radius=4)
        return c.finish()
    return c.finish()


def side_standing(mode, p, focus):
    c = Canvas()
    if mode == "leg_press":
        c.line([(80, 184), (147, 108)], 9, EQUIPMENT)
        c.line([(79, 185), (172, 185)], 8, EQUIPMENT)
        c.line([(283, 38), (303, 202)], 10, EQUIPMENT)
        c.head((113, 84))
        c.line([(135, 99), (179, 149)], 31, BODY_LIGHT, LINE)
        c.oval((159, 130, 191, 162), BODY, LINE)
        knee = (222 - 24 * p, 119 + 35 * p)
        foot = (283, 76 + 24 * p)
        c.line([(177, 148), knee, foot], 19, MUSCLE, MUSCLE_DARK)
        c.line([(181, 147), (220 - 22 * p, 135 + 30 * p), (283, 98 + 24 * p)], 15, BODY, LINE)
        c.rect((272, 58, 289, 122), EQUIPMENT, radius=3)
        return c.finish()
    if mode == "leg_extension":
        c.rect((98, 132, 218, 143), EQUIPMENT, radius=3)
        c.line([(104, 139), (99, 215)], 6, EQUIPMENT)
        c.line([(207, 140), (213, 215)], 6, EQUIPMENT)
        c.head((148, 54))
        c.line([(153, 72), (170, 123)], 27, BODY_LIGHT, LINE)
        c.line([(173, 123), (222, 137)], 23, MUSCLE, MUSCLE_DARK)
        ankle = (238 + 36 * p, 183 - 49 * p)
        c.line([(222, 137), ankle], 17, BODY, LINE)
        c.rect((ankle[0] - 5, ankle[1] - 8, ankle[0] + 13, ankle[1] + 9), EQUIPMENT, radius=4)
        return c.finish()
    if mode in {"hinge", "row", "back_extension"}:
        lean = 0.25 + (0.7 * p if mode != "row" else 0.3)
        hip = (208, 138)
        shoulder = (208 - 71 * lean, 66 + 57 * lean)
        head = (shoulder[0] - 10, shoulder[1] - 29)
        c.line([hip, (205, 187), (212, 216)], 20, MUSCLE if focus == "hamstrings" else BODY, LINE)
        c.line([hip, (226, 184), (242, 216)], 18, MUSCLE if focus == "hamstrings" else BODY, LINE)
        c.oval((197, 131, 222, 153), BODY, LINE)
        c.line([hip, shoulder], 30, MUSCLE if focus == "back" else BODY_LIGHT, LINE)
        c.head(head)
        elbow = (shoulder[0] + 1 + (24 * p if mode == "row" else 0), shoulder[1] + 43 - (26 * p if mode == "row" else 0))
        hand = (elbow[0] + 4, elbow[1] + 39 - (15 * p if mode == "row" else 0))
        c.line([shoulder, elbow, hand], 14, BODY, LINE)
        if mode != "back_extension":
            c.dumbbell(hand)
        else:
            c.line([shoulder, (shoulder[0] - 19, shoulder[1] + 18)], 11, BODY, LINE)
        return c.finish()
    if mode == "walk":
        c.rect((68, 209, 300, 218), EQUIPMENT, radius=3)
        x = 180
        swing = math.sin(2 * math.pi * p)
        c.line([(x - 12, 137), (x - 21 - 20 * swing, 179), (x - 24 - 30 * swing, 210)], 18, BODY, LINE)
        c.line([(x + 12, 137), (x + 21 + 20 * swing, 179), (x + 24 + 30 * swing, 210)], 18, BODY, LINE)
        c.line([(x, 70), (x, 138)], 30, BODY_LIGHT, LINE)
        c.head((x, 42))
        c.line([(x - 16, 79), (x - 27 + 16 * swing, 115), (x - 21 + 18 * swing, 145)], 13, BODY, LINE)
        c.line([(x + 16, 79), (x + 27 - 16 * swing, 115), (x + 21 - 18 * swing, 145)], 13, BODY, LINE)
        return c.finish()
    return c.finish()


ANIMATIONS = {
    "demo_plank": ("plank", "abs"),
    "demo_crunch": ("crunch", "abs"),
    "demo_wrist_curl": ("wrist", "forearms"),
    "demo_curl": ("curl", "biceps"),
    "demo_upright_row": ("upright", "back"),
    "demo_pulldown": ("pulldown", "back"),
    "demo_row": ("row", "back"),
    "demo_back_extension": ("back_extension", "back"),
    "demo_press": ("press", "shoulders"),
    "demo_lateral_raise": ("lateral", "shoulders"),
    "demo_bench_press": ("bench", "chest"),
    "demo_incline_press": ("incline", "chest"),
    "demo_fly": ("fly", "chest"),
    "demo_pushup": ("pushup", "chest"),
    "demo_neck": ("neck", "neck"),
    "demo_shrug": ("shrug", "traps"),
    "demo_triceps": ("triceps", "triceps"),
    "demo_overhead_triceps": ("overhead_triceps", "triceps"),
    "demo_abductor": ("abductor", "glutes"),
    "demo_adductor": ("adductor", "quads"),
    "demo_hip_thrust": ("hip_thrust", "glutes"),
    "demo_squat": ("squat", "quads"),
    "demo_sumo_squat": ("sumo", "glutes"),
    "demo_kickback": ("kickback", "glutes"),
    "demo_calf_raise": ("calf", "calves"),
    "demo_leg_curl": ("leg_curl", "hamstrings"),
    "demo_hinge": ("hinge", "hamstrings"),
    "demo_leg_press": ("leg_press", "quads"),
    "demo_leg_extension": ("leg_extension", "quads"),
    "demo_walk": ("walk", "cardio"),
    "demo_jumping_jack": ("jack", "full"),
    "demo_stretch": ("stretch", "full"),
}


def draw_scene(mode, phase, focus):
    if mode in {"bench", "incline", "fly"}:
        return bench(mode, phase, focus)
    if mode in {"plank", "pushup", "crunch", "hip_thrust", "kickback", "leg_curl"}:
        return horizontal(mode, phase, focus)
    if mode in {"leg_press", "leg_extension", "hinge", "row", "back_extension", "walk"}:
        return side_standing(mode, phase, focus)
    return standing(mode, phase, focus)


def main():
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for stem, (mode, focus) in ANIMATIONS.items():
        frames = []
        for index in range(FRAMES):
            phase = (1 - math.cos(2 * math.pi * index / FRAMES)) / 2
            frames.append(draw_scene(mode, phase, focus).quantize(colors=64))
        path = OUTPUT / f"{stem}.gif"
        frames[0].save(path, save_all=True, append_images=frames[1:],
                       loop=0, duration=DURATION, disposal=2, optimize=True)
        print(path.name, path.stat().st_size)


if __name__ == "__main__":
    main()
