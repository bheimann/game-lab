# Turns a big "pixel art" picture (e.g. a JPG on a dark background) into a small game sprite:
# removes the background, shrinks it, snaps colours to our palette (art-source/palette.png) and adds an outline.
#
# Usage (from the repo root):
#   Godot_console.exe --headless --script tools/pixelize.gd -- <input> <output.png> <height> [dark_threshold]
#
# <height> is the final sprite height in pixels, including the 1px outline.
extends SceneTree

const PALETTE := "art-source/palette.png"
const OUTLINE := Color8(20, 24, 46)


func _init() -> void:
	var args := OS.get_cmdline_user_args()
	if args.size() < 3:
		push_error("usage: -- <input> <output.png> <height> [dark_threshold]")
		quit(1)
		return
	var dark := float(args[3]) if args.size() > 3 else 0.22

	var src := Image.load_from_file(args[0])
	src.convert(Image.FORMAT_RGBA8)
	var background := _flood_background(src, dark)
	var box := _foreground_box(src, background)

	var inner_h := int(args[2]) - 2
	var scale := float(box.size.y) / inner_h
	var inner_w := int(ceil(box.size.x / scale))
	var palette := _load_palette()

	var out := Image.create_empty(inner_w + 2, inner_h + 2, false, Image.FORMAT_RGBA8)
	out.fill(Color(0, 0, 0, 0))
	for ty in inner_h:
		for tx in inner_w:
			var c := _average(src, background, box, scale, tx, ty)
			if c.a >= 0.45:
				out.set_pixel(tx + 1, ty + 1, _nearest(palette, c))
	_add_outline(out)

	out.save_png(args[1])
	print("wrote %s (%dx%d)" % [args[1], out.get_width(), out.get_height()])
	quit()


# Background = dark pixels connected to the image border, so dark pupils inside the body survive.
func _flood_background(img: Image, dark: float) -> PackedByteArray:
	var w := img.get_width()
	var h := img.get_height()
	var bg := PackedByteArray()
	bg.resize(w * h)
	var stack: Array[int] = []
	for x in w:
		stack.append(x)
		stack.append((h - 1) * w + x)
	for y in h:
		stack.append(y * w)
		stack.append(y * w + w - 1)
	while not stack.is_empty():
		var i: int = stack.pop_back()
		if bg[i] == 1:
			continue
		var c := img.get_pixel(i % w, i / w)
		if max(c.r, c.g, c.b) > dark:
			continue
		bg[i] = 1
		var x := i % w
		var y := i / w
		if x > 0: stack.append(i - 1)
		if x < w - 1: stack.append(i + 1)
		if y > 0: stack.append(i - w)
		if y < h - 1: stack.append(i + w)
	return bg


func _foreground_box(img: Image, bg: PackedByteArray) -> Rect2i:
	var w := img.get_width()
	var min_x := w
	var min_y := img.get_height()
	var max_x := -1
	var max_y := -1
	for i in bg.size():
		if bg[i] == 0:
			min_x = min(min_x, i % w)
			max_x = max(max_x, i % w)
			min_y = min(min_y, i / w)
			max_y = max(max_y, i / w)
	return Rect2i(min_x, min_y, max_x - min_x + 1, max_y - min_y + 1)


# Average colour of the foreground pixels under one target pixel; alpha = how much is foreground.
func _average(img: Image, bg: PackedByteArray, box: Rect2i, scale: float, tx: int, ty: int) -> Color:
	var w := img.get_width()
	var x0 := box.position.x + int(tx * scale)
	var x1 := mini(box.position.x + int((tx + 1) * scale), box.end.x)
	var y0 := box.position.y + int(ty * scale)
	var y1 := mini(box.position.y + int((ty + 1) * scale), box.end.y)
	var sum := Vector3.ZERO
	var count := 0
	var total := 0
	for y in range(y0, y1):
		for x in range(x0, x1):
			total += 1
			if bg[y * w + x] == 0:
				var c := img.get_pixel(x, y)
				sum += Vector3(c.r, c.g, c.b)
				count += 1
	if count == 0:
		return Color(0, 0, 0, 0)
	sum /= count
	return Color(sum.x, sum.y, sum.z, float(count) / total)


func _load_palette() -> Array[Color]:
	var img := Image.load_from_file(PALETTE)
	var colours: Array[Color] = []
	for y in img.get_height():
		for x in img.get_width():
			var c := img.get_pixel(x, y)
			if c.a > 0.5 and not colours.has(c):
				colours.append(c)
	return colours


func _nearest(palette: Array[Color], c: Color) -> Color:
	var best := palette[0]
	var best_d := INF
	for p in palette:
		var d := Vector3(p.r - c.r, p.g - c.g, p.b - c.b).length_squared()
		if d < best_d:
			best_d = d
			best = p
	return best


func _add_outline(img: Image) -> void:
	var copy := img.duplicate() as Image
	for y in img.get_height():
		for x in img.get_width():
			if copy.get_pixel(x, y).a > 0:
				continue
			for d in [Vector2i(1, 0), Vector2i(-1, 0), Vector2i(0, 1), Vector2i(0, -1)]:
				var n: Vector2i = Vector2i(x, y) + d
				if n.x >= 0 and n.y >= 0 and n.x < img.get_width() and n.y < img.get_height() \
						and copy.get_pixel(n.x, n.y).a > 0:
					img.set_pixel(x, y, OUTLINE)
					break
