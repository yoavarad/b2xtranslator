#!/usr/bin/env python3
"""Deterministically generate the large-tier perf corpus (stdlib only).

Each large file is a small-tier fixture re-serialised as an OLE compound file
whose real content is scaled to ~10 MB: .doc gets 100k extra paragraphs,
.xls 65k rows x 8 cells, .ppt 9k copies of the slide. Output is
byte-identical on every OS.

    python generate.py            # write large/*, verify against manifest.json
    python generate.py --update   # regenerate and rewrite hashes in manifest.json
"""
import hashlib
import json
import os
import struct
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
SS, MS = 512, 64
END, FREE, FATSECT, DIFSECT, NOSTREAM = 0xFFFFFFFE, 0xFFFFFFFF, 0xFFFFFFFD, 0xFFFFFFFC, 0xFFFFFFFF
DOC_PARAS = 100000
XLS_ROWS, XLS_COLS = 65000, 8
PPT_SLIDES = 9000
LARGE = {"large.doc": "simple.doc", "large.xls": "simple.xls", "large.ppt": "simple.ppt"}


def read_cfb(data):
    assert data[:8] == bytes.fromhex("D0CF11E0A1B11AE1")
    nfat, dir_start = struct.unpack_from("<II", data, 0x2C)
    cutoff, mf_start, nmf, _, ndif = struct.unpack_from("<IIIII", data, 0x38)
    assert ndif == 0, "source DIFAT not supported"

    def sect(i):
        return data[SS * (i + 1):SS * (i + 2)]

    fat = []
    for s in struct.unpack_from("<109I", data, 0x4C)[:nfat]:
        fat += struct.unpack("<128I", sect(s))

    def chain(s):
        out = b""
        while s != END:
            out += sect(s)
            s = fat[s]
        return out

    raw = chain(dir_start)
    ents = []
    for i in range(len(raw) // 128):
        r = raw[i * 128:(i + 1) * 128]
        nl = struct.unpack_from("<H", r, 64)[0]
        left, right, child = struct.unpack_from("<III", r, 68)
        start, size = struct.unpack_from("<IQ", r, 116)
        ents.append(dict(name=r[:max(nl - 2, 0)].decode("utf-16-le"), type=r[66], color=r[67], left=left,
                         right=right, child=child, clsid=r[80:96], state=r[96:100], times=r[100:116],
                         start=start, size=size, data=None))
    minifat = list(struct.unpack("<%dI" % (len(chain(mf_start)) // 4), chain(mf_start))) if nmf else []
    mini = chain(ents[0]["start"]) if ents[0]["start"] != END else b""
    for e in ents:
        if e["type"] != 2:
            continue
        if e["size"] < cutoff:
            s, buf = e["start"], b""
            while s != END:
                buf += mini[s * MS:(s + 1) * MS]
                s = minifat[s]
        else:
            buf = chain(e["start"])
        e["data"] = buf[:e["size"]]
    return ents


def name_key(e):
    return (len(e["name"]), e["name"].upper())


def write_cfb(ents):
    sectors, fat = [], []

    def alloc(buf):
        n = -(-len(buf) // SS)
        if n == 0:
            return END
        start = len(sectors)
        buf = buf.ljust(n * SS, b"\0")
        for i in range(n):
            sectors.append(buf[i * SS:(i + 1) * SS])
            fat.append(start + i + 1 if i < n - 1 else END)
        return start

    ministream, minifat = b"", []
    for e in ents:
        if e["type"] != 2:
            continue
        d = e["data"]
        e["size"] = len(d)
        if not d:
            e["start"] = END
        elif len(d) < 4096:
            first, k = len(ministream) // MS, -(-len(d) // MS)
            ministream += d.ljust(k * MS, b"\0")
            minifat += [first + i + 1 for i in range(k - 1)] + [END]
            e["start"] = first
        else:
            e["start"] = alloc(d)
    ents[0]["size"] = len(ministream)
    ents[0]["start"] = alloc(ministream) if ministream else END
    mf = b"".join(struct.pack("<I", x) for x in minifat)
    mf_start = alloc(mf.ljust(-(-len(mf) // SS) * SS, b"\xff")) if mf else END
    nmf = -(-len(mf) // SS)

    rec = b""
    for e in ents:
        nm = e["name"].encode("utf-16-le") + b"\0\0" if e["name"] else b""
        rec += (nm.ljust(64, b"\0") + struct.pack("<H", len(nm)) + bytes([e["type"], e["color"]])
                + struct.pack("<III", e["left"], e["right"], e["child"]) + e["clsid"] + e["state"]
                + e["times"] + struct.pack("<IQ", e["start"], e["size"]))
    while len(rec) % SS:  # pad with empty (unallocated) entries
        rec += b"\0" * 66 + b"\0\0" + struct.pack("<III", NOSTREAM, NOSTREAM, NOSTREAM) + b"\0" * 44
    dir_start = alloc(rec)

    nfat = 1
    while True:
        ndif = max(0, -(-(nfat - 109) // 127))
        if len(fat) + nfat + ndif <= nfat * 128:
            break
        nfat += 1
    fat_ids = list(range(len(fat), len(fat) + nfat))
    fat += [FATSECT] * nfat
    dif_ids = list(range(len(fat), len(fat) + ndif))
    fat += [DIFSECT] * ndif
    fat += [FREE] * (nfat * 128 - len(fat))
    fatbytes = b"".join(struct.pack("<I", x) for x in fat)

    head_fat = fat_ids[:109]
    head_fat += [FREE] * (109 - len(head_fat))
    header = (bytes.fromhex("D0CF11E0A1B11AE1") + b"\0" * 16 + struct.pack("<HHHHH", 0x3E, 3, 0xFFFE, 9, 6)
              + b"\0" * 6 + struct.pack("<I", 0) + struct.pack("<II", nfat, dir_start)
              + struct.pack("<IIIIII", 0, 4096, mf_start, nmf, dif_ids[0] if dif_ids else END, ndif)
              + b"".join(struct.pack("<I", x) for x in head_fat))
    out = [header] + sectors
    out += [fatbytes[i * SS:(i + 1) * SS] for i in range(nfat)]
    rest = fat_ids[109:]
    for j in range(ndif):
        chunk = rest[j * 127:(j + 1) * 127]
        chunk += [FREE] * (127 - len(chunk))
        out.append(b"".join(struct.pack("<I", x) for x in chunk)
                   + struct.pack("<I", dif_ids[j + 1] if j + 1 < ndif else END))
    return b"".join(out)


def stream(ents, name):
    return next(e for e in ents if e["name"] == name)


def fkp_runs(wd, page, papx):
    """Parse a CHPX/PAPX FKP page into [(start, end, bx_phe, payload)]."""
    o = page * 512
    n = wd[o + 511]
    fc = struct.unpack_from("<%dI" % (n + 1), wd, o)
    bxs = 13 if papx else 1
    runs = []
    for j in range(n):
        bx = bytes(wd[o + 4 * (n + 1) + bxs * j:o + 4 * (n + 1) + bxs * (j + 1)])
        pay = b""
        if bx[0]:
            at = o + bx[0] * 2
            cb = wd[at]
            size = (2 + 2 * wd[at + 1] if cb == 0 else 2 * cb) if papx else 1 + cb
            pay = bytes(wd[at:at + size])
        runs.append((fc[j], fc[j + 1], bx[1:], pay))
    return runs


def fkp_pages(runs, papx):
    """Pack runs (start, end, phe, payload) into 512-byte FKP pages; yields (page, first_fc, last_fc)."""
    bxs = 13 if papx else 1
    i = 0
    while i < len(runs):
        c, ptr = 0, 511
        while i + c < len(runs):
            size = len(runs[i + c][3])
            if 4 * (c + 2) + bxs * (c + 1) > ptr - size - (size & 1) and c:
                break
            ptr -= (size + 1) & ~1
            c += 1
        part = runs[i:i + c]
        page, ptr = bytearray(512), 511
        struct.pack_into("<%dI" % (c + 1), page, 0, *([r[0] for r in part] + [part[-1][1]]))
        for j, (_, _, phe, pay) in enumerate(part):
            if pay:
                ptr = (ptr - len(pay)) & ~1
                page[ptr:ptr + len(pay)] = pay
            page[4 * (c + 1) + bxs * j] = ptr // 2 if pay else 0
            if papx:
                page[4 * (c + 1) + bxs * j + 1:4 * (c + 1) + bxs * (j + 1)] = phe
        page[511] = c
        yield bytes(page), part[0][0], part[-1][1]
        i += c


def scale_doc(ents):
    """Insert DOC_PARAS paragraphs after the main text. The text is relocated as one contiguous
    piece (the mapper assumes contiguous fcs) and the CHPX/PAPX FKPs are rebuilt with one run per paragraph."""
    w, t = stream(ents, "WordDocument"), stream(ents, "1Table")
    wd, td = bytearray(w["data"]), bytearray(t["data"])
    csw = struct.unpack_from("<H", wd, 32)[0]
    lw = 34 + csw * 2 + 2
    fb = lw + struct.unpack_from("<H", wd, lw - 2)[0] * 4 + 2

    def fib(i):
        return struct.unpack_from("<II", wd, fb + 8 * i)

    ccp = struct.unpack_from("<i", wd, lw + 12)[0]
    text = b"".join(b"Perf paragraph %06d: the quick brown fox jumps over the lazy dog.\r" % i
                    for i in range(DOC_PARAS))
    n, para = len(text), len(text) // DOC_PARAS
    clx_fc, _ = fib(33)
    sed_fc = fib(6)[0]
    assert fib(12)[1] == 12 and fib(13)[1] == 12 and td[clx_fc] == 2, "unexpected simple.doc layout"
    pcd_fc = struct.unpack_from("<I", td, clx_fc + 5 + 8 + 2)[0]
    total = struct.unpack_from("<I", td, clx_fc + 5 + 4)[0]
    assert struct.unpack_from("<I", td, sed_fc + 4)[0] == total  # section end CP
    a0 = (pcd_fc & 0x3FFFFFFF) // 2
    x0 = a0 + ccp  # fc where the main text ends

    base = -(-len(wd) // 512) * 512
    wd += b"\0" * (base - len(wd)) + bytes(wd[a0:x0]) + text + bytes(wd[x0:a0 + total])
    wd += b"\0" * (-len(wd) % 512)
    new_x0 = base + ccp
    plcs = {}
    for idx, papx in ((12, False), (13, True)):
        plc_fc = fib(idx)[0]
        pn0 = struct.unpack_from("<I", td, plc_fc + 8)[0]
        runs = []
        for s, e, phe, pay in fkp_runs(wd, pn0, papx):
            assert s >= a0 and e <= a0 + total
            if e <= x0:
                runs.append((s - a0 + base, e - a0 + base, phe, pay))
        runs += [(new_x0 + i * para, new_x0 + (i + 1) * para, b"\0" * 12, b"") for i in range(DOC_PARAS)]
        for s, e, phe, pay in fkp_runs(wd, pn0, papx):
            if s >= x0:
                runs.append((s - a0 + base + n, e - a0 + base + n, phe, pay))
        assert runs[0][0] == base and all(r[1] == q[0] for r, q in zip(runs, runs[1:]))
        afc, pns = [], []
        for page, first, last in fkp_pages(runs, papx):
            pns.append(len(wd) // 512)
            wd += page
            afc.append(first)
        afc.append(last)
        plcs[idx] = struct.pack("<%dI%dI" % (len(afc), len(pns)), *afc, *pns)
    clx = (b"\x02" + struct.pack("<I", 4 * 2 + 8) + struct.pack("<II", 0, total + n)
           + struct.pack("<HIH", 0x98, (base * 2) | 0x40000000, 0))
    struct.pack_into("<I", td, sed_fc + 4, total + n)
    for idx, plc in plcs.items():
        struct.pack_into("<II", wd, fb + 8 * idx, len(td), len(plc))
        td += plc
    struct.pack_into("<II", wd, fb + 8 * 33, len(td), len(clx))
    td += clx
    struct.pack_into("<II", wd, 0x18, base, base + total + n)
    struct.pack_into("<i", wd, lw, len(wd))
    struct.pack_into("<i", wd, lw + 12, ccp + n)
    w["data"], t["data"] = bytes(wd), bytes(td)


def scale_xls(ents):
    """Fill the empty last worksheet with XLS_ROWS rows of XLS_COLS NUMBER / LABELSST cells."""
    e = stream(ents, "Workbook")
    d = bytearray(e["data"])
    p, nbof, dims, win = 0, 0, None, None
    while p < len(d):
        typ, ln = struct.unpack_from("<HH", d, p)
        nbof += typ == 0x809
        if nbof == 3 and typ == 0x200:
            dims = p
        if nbof == 3 and typ == 0x23E:
            win = p
        p += 4 + ln
    assert dims and win, "unexpected simple.xls layout"
    struct.pack_into("<IIHH", d, dims + 4, 0, XLS_ROWS, 0, XLS_COLS)
    out = bytearray()
    for r0 in range(0, XLS_ROWS, 32):
        rows = range(r0, min(r0 + 32, XLS_ROWS))
        for r in rows:
            out += struct.pack("<HHHHHHHHHH", 0x208, 16, r, 0, XLS_COLS, 0xFF, 0, 0, 0, 0x0F)
        for r in rows:
            for c in range(XLS_COLS):
                if c % 2:
                    out += struct.pack("<HHHHHI", 0xFD, 10, r, c, 0x0F, (r + c) % 4)
                else:
                    out += struct.pack("<HHHHHd", 0x203, 14, r, c, 0x0F, r * 1.5 + c)
    e["data"] = bytes(d[:win]) + bytes(out) + bytes(d[win:])


def scale_ppt(ents):
    """Append PPT_SLIDES-1 copies of the slide (own SlidePersistAtom + persist entry each)."""
    e, cu = stream(ents, "PowerPoint Document"), stream(ents, "Current User")
    d = e["data"]
    SLIDE, SLIDES_END, PDIR, SPL = 0xD41, 0x11AC, 0x11AC, 0x471
    assert struct.unpack_from("<HHI", d, SLIDE)[1:] == (0x3EE, 0x463)
    assert struct.unpack_from("<HHI", d, SPL)[1:] == (0xFF0, 28) and struct.unpack_from("<HHI", d, PDIR)[1] == 0x1772
    slide, k = d[SLIDE:SLIDES_END], PPT_SLIDES - 1
    spas = b"".join(struct.pack("<HHIIIIII", 0, 0x3F3, 20, 4 + i, 4, 0, 257 + i, 0) for i in range(k))
    head = bytearray(d[:SPL + 8 + 28])
    struct.pack_into("<I", head, SPL + 4, 28 + len(spas))
    struct.pack_into("<I", head, 4, struct.unpack_from("<I", d, 4)[0] + len(spas))
    body = bytes(head) + spas + d[SPL + 8 + 28:SLIDES_END] + slide * k
    first = SLIDES_END + len(spas)
    offs = [0, 0x49D + len(spas), SLIDE + len(spas)] + [first + i * len(slide) for i in range(k)]
    pdir_off = len(body)
    ids = [(1, offs[:3])] + [(4 + j, offs[3 + j:3 + j + 4095]) for j in range(0, k, 4095)]
    payload = b"".join(struct.pack("<I", pid | (len(o) << 20)) + b"".join(struct.pack("<I", x) for x in o)
                       for pid, o in ids)
    pda = struct.pack("<HHI", 0, 0x1772, len(payload)) + payload
    uea = bytearray(d[PDIR + 8 + 16:])  # original UserEditAtom (header + payload)
    struct.pack_into("<I", uea, 8, 256 + k)
    struct.pack_into("<I", uea, 8 + 12, pdir_off)
    struct.pack_into("<I", uea, 8 + 20, 3 + k)
    e["data"] = body + pda + bytes(uea)
    cud = bytearray(cu["data"])
    struct.pack_into("<I", cud, 16, len(body) + len(pda))
    cu["data"] = bytes(cud)


SCALERS = {"large.doc": scale_doc, "large.xls": scale_xls, "large.ppt": scale_ppt}


def sha(path):
    with open(path, "rb") as f:
        return hashlib.sha256(f.read()).hexdigest()


def main():
    update = "--update" in sys.argv
    manifest_path = os.path.join(HERE, "manifest.json")
    with open(manifest_path) as f:
        manifest = json.load(f)
    os.makedirs(os.path.join(HERE, "large"), exist_ok=True)
    bad = 0
    for out_name, src in LARGE.items():
        with open(os.path.join(HERE, "small", src), "rb") as f:
            ents = read_cfb(f.read())
        SCALERS[out_name](ents)
        path = os.path.join(HERE, "large", out_name)
        with open(path, "wb") as f:
            f.write(write_cfb(ents))
        entry = manifest.setdefault("large/" + out_name, {})
        if update:
            entry["sha256"] = sha(path)
        ok = entry.get("sha256") == sha(path)
        print("%s large/%s %d bytes" % ("OK " if ok else "BAD", out_name, os.path.getsize(path)))
        bad += not ok
    for name, entry in manifest.items():
        if name.startswith("small/"):
            if update:
                entry["sha256"] = sha(os.path.join(HERE, name))
            ok = entry.get("sha256") == sha(os.path.join(HERE, name))
            print("%s %s" % ("OK " if ok else "BAD", name))
            bad += not ok
    if update:
        with open(manifest_path, "w") as f:
            json.dump(manifest, f, indent=2, sort_keys=True)
            f.write("\n")
        return 0
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
