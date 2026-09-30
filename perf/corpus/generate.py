#!/usr/bin/env python3
"""Deterministically generate the large-tier perf corpus (stdlib only).

Each large file is a small-tier fixture re-serialised as an OLE compound file
with one extra stream "PerfPadding" of seeded pseudo-random bytes (~10 MB).
Output is byte-identical on every OS.

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
TARGET = 10 * 1024 * 1024
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


def insert(ents, new):
    """Attach `new` as a leaf of the root's directory tree (ordering per CFB spec)."""
    ents.append(new)
    idx = len(ents) - 1
    cur = ents[0]["child"]
    while True:
        side = "left" if name_key(new) < name_key(ents[cur]) else "right"
        if ents[cur][side] == NOSTREAM:
            ents[cur][side] = idx
            return
        cur = ents[cur][side]


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


def padding(nbytes, seed):
    out, i = bytearray(), 0
    while len(out) < nbytes:
        out += hashlib.sha256(("%s:%d" % (seed, i)).encode()).digest()
        i += 1
    return bytes(out[:nbytes])


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
        insert(ents, dict(name="PerfPadding", type=2, color=0, left=NOSTREAM, right=NOSTREAM, child=NOSTREAM,
                          clsid=b"\0" * 16, state=b"\0" * 4, times=b"\0" * 16, start=0, size=0,
                          data=padding(TARGET, out_name)))
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
