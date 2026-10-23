"""中文字符/双字词 BM25 检索，返回可追溯的 Markdown 段落。"""
import math
import re
import sqlite3
import threading
from collections import Counter
from contextlib import closing
from pathlib import Path
from .settings import PACKAGE


def tokens(text, normalize_identifiers=True):
    text = text.lower()
    words = re.findall(r"[a-z0-9_]+", text)
    if normalize_identifiers:
        # 错误码和偏移在问题中常省略 0x；文档与查询使用相同的规范形式。
        words.extend("hex:" + match for match in re.findall(r"(?<![a-z0-9_])(?:0x)?([a-f0-9]{6,16})(?![a-z0-9_])", text))
    for sentence in re.findall(r"[\u3400-\u9fff]+", text):
        words.extend(sentence)
        words.extend(sentence[i:i + 2] for i in range(len(sentence) - 1))
    return words


class Knowledge:
    def __init__(self, path):
        path.parent.mkdir(parents=True, exist_ok=True)
        self.path = path
        self.lock = threading.Lock()
        self._initialize()

    def _initialize(self):
        with self.lock, closing(sqlite3.connect(self.path)) as db, db:
            db.execute("CREATE TABLE IF NOT EXISTS chunks (id TEXT PRIMARY KEY, source TEXT, title TEXT, text TEXT)")
            db.execute("DELETE FROM chunks")
            for file in sorted((PACKAGE / "knowledge").glob("*.md")):
                section, body, start = file.stem, [], 1
                lines = file.read_text(encoding="utf-8").splitlines()
                def save():
                    content = "\n".join(body).strip()
                    if content:
                        ident = f"{file.stem}:{start}"
                        db.execute("INSERT INTO chunks VALUES(?,?,?,?)", (ident, f"knowledge/{file.name}:L{start}", section, content))
                for index, line in enumerate(lines, 1):
                    if line.startswith("#"):
                        save()
                        section, body, start = line.lstrip("# "), [], index
                    else:
                        body.append(line)
                save()

    def sections(self):
        """评测用的逻辑段落标识不依赖行号；实际引用仍保留路径与行号。"""
        with closing(sqlite3.connect(self.path)) as db:
            rows = db.execute("SELECT id,source,title,text FROM chunks ORDER BY id").fetchall()
        return [dict(id=row[0], source=row[1], title=row[2], text=row[3],
                     section_id=f"{Path(row[1].split(':L')[0]).stem}#{row[2]}") for row in rows]

    def search(self, query, limit=4, *, normalize_identifiers=True):
        with closing(sqlite3.connect(self.path)) as db:
            rows = db.execute("SELECT id,source,title,text FROM chunks ORDER BY id").fetchall()
        docs = [Counter(tokens(row[2] + " " + row[3], normalize_identifiers)) for row in rows]
        sizes = [sum(doc.values()) for doc in docs]
        avg = sum(sizes) / max(len(sizes), 1)
        terms = set(tokens(query, normalize_identifiers))
        results = []
        for row, doc, size in zip(rows, docs, sizes):
            score = 0.0
            for term in terms & doc.keys():
                freq = sum(term in candidate for candidate in docs)
                idf = math.log(1 + (len(docs) - freq + .5) / (freq + .5))
                tf = doc[term]
                score += idf * tf * 2.2 / (tf + 1.2 * (.25 + .75 * size / max(avg, 1)))
            if score > 0:
                results.append(dict(id=row[0], source=row[1], title=row[2], text=row[3], score=round(score, 4)))
        return sorted(results, key=lambda row: (-row["score"], row["id"]))[:limit]
