from pathlib import Path

from reportlab.lib import colors
from reportlab.lib.pagesizes import A4
from reportlab.lib.utils import ImageReader, simpleSplit
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas


ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "output" / "pdf" / "AI_활용_기술_문서_최종.pdf"
IMG = ROOT / "output" / "docs" / "images"
FONT = r"C:\Windows\Fonts\malgun.ttf"
BOLD = r"C:\Windows\Fonts\malgunbd.ttf"
W, H = A4
M = 46

NAVY = colors.HexColor("#14283C")
BLUE = colors.HexColor("#2587B8")
TEAL = colors.HexColor("#12A5A1")
SKY = colors.HexColor("#DDEFF7")
PALE = colors.HexColor("#F3F8FB")
GRAY = colors.HexColor("#64717C")
LINE = colors.HexColor("#C7D6DF")
ORANGE = colors.HexColor("#F3B44A")
GREEN = colors.HexColor("#58A56A")


def register_fonts():
    pdfmetrics.registerFont(TTFont("KR", FONT))
    pdfmetrics.registerFont(TTFont("KR-Bold", BOLD))


def text(c, x, y, value, size=9, color=NAVY, font="KR"):
    c.setFont(font, size)
    c.setFillColor(color)
    c.drawString(x, y, value)


def para(c, x, y, value, width, size=8.7, leading=13.2, color=NAVY, font="KR"):
    c.setFont(font, size)
    c.setFillColor(color)
    lines = []
    for raw in value.split("\n"):
        lines.extend(simpleSplit(raw, font, size, width) or [""])
    for line in lines:
        c.drawString(x, y, line)
        y -= leading
    return y


def page_header(c, page, section, title, subtitle=""):
    c.setFillColor(NAVY)
    c.rect(0, H - 28, W, 28, fill=1, stroke=0)
    text(c, M, H - 19, "IN THE ARENA | AI UTILIZATION TECHNICAL DOCUMENT", 7.4, colors.white, "KR-Bold")
    text(c, W - 62, H - 19, f"{page:02d} / 10", 7.4, colors.white, "KR-Bold")
    text(c, M, H - 55, section, 8.2, TEAL, "KR-Bold")
    text(c, M, H - 82, title, 19, NAVY, "KR-Bold")
    if subtitle:
        para(c, M, H - 101, subtitle, W - 2 * M, 8.2, 11, GRAY)
    c.setStrokeColor(LINE)
    c.line(M, H - 119, W - M, H - 119)


def footer(c, note):
    c.setStrokeColor(LINE)
    c.line(M, 32, W - M, 32)
    text(c, M, 18, note, 6.6, GRAY)
    text(c, W - 112, 18, "확인일 2026.08.10", 6.6, GRAY)


def section_title(c, y, title, num=None):
    if num is not None:
        c.setFillColor(BLUE)
        c.circle(M + 9, y - 2, 9, fill=1, stroke=0)
        text(c, M + 5.3, y - 5.2, str(num), 7.5, colors.white, "KR-Bold")
        text(c, M + 25, y - 6, title, 12.2, NAVY, "KR-Bold")
    else:
        text(c, M, y - 4, title, 12.2, NAVY, "KR-Bold")
    return y - 25


def rounded_box(c, x, y, w, h, title="", body="", fill=colors.white, stroke=LINE, body_size=7.6):
    c.setFillColor(fill)
    c.setStrokeColor(stroke)
    c.roundRect(x, y - h, w, h, 7, fill=1, stroke=1)
    if title:
        text(c, x + 11, y - 18, title, 8.7, NAVY, "KR-Bold")
    if body:
        para(c, x + 11, y - 34, body, w - 22, body_size, body_size + 3.2, GRAY)


def arrow(c, x1, y, x2, color=TEAL):
    c.setStrokeColor(color)
    c.setFillColor(color)
    c.setLineWidth(1.5)
    c.line(x1, y, x2, y)
    c.line(x2 - 5, y + 4, x2, y)
    c.line(x2 - 5, y - 4, x2, y)
    c.setLineWidth(1)


def flow(c, y, labels, x=M, total_w=None, h=44, font_size=7.2):
    total_w = total_w or (W - 2 * M)
    gap = 8
    bw = (total_w - gap * (len(labels) - 1)) / len(labels)
    for i, label in enumerate(labels):
        fill = SKY if i % 2 == 0 else colors.white
        c.setFillColor(fill)
        c.setStrokeColor(BLUE)
        c.roundRect(x, y - h, bw, h, 6, fill=1, stroke=1)
        lines = label.split("\n")
        base = y - 17 if len(lines) == 1 else y - 14
        for j, line in enumerate(lines):
            line_w = pdfmetrics.stringWidth(line, "KR-Bold", font_size)
            text(c, x + (bw - line_w) / 2, base - j * 11, line, font_size, NAVY, "KR-Bold")
        if i < len(labels) - 1:
            arrow(c, x + bw + 1, y - h / 2, x + bw + gap - 1)
        x += bw + gap


def draw_image_fit(c, path, x, y, w, h, bg=PALE):
    path = Path(path)
    c.setFillColor(bg)
    c.setStrokeColor(LINE)
    c.roundRect(x, y - h, w, h, 6, fill=1, stroke=1)
    if not path.exists():
        text(c, x + 10, y - 22, "이미지 파일을 찾을 수 없음", 7, GRAY)
        return
    img = ImageReader(str(path))
    iw, ih = img.getSize()
    scale = min((w - 16) / iw, (h - 16) / ih)
    dw, dh = iw * scale, ih * scale
    c.drawImage(img, x + (w - dw) / 2, y - h + (h - dh) / 2, dw, dh, mask="auto")


def source_table(c, x, y, widths, rows):
    headers = ["구분", "명칭", "사용 목적", "출처·비고"]
    head_h = 22
    c.setFillColor(NAVY)
    c.setStrokeColor(LINE)
    c.rect(x, y - head_h, sum(widths), head_h, fill=1, stroke=1)
    left = x
    for head, width in zip(headers, widths):
        text(c, left + 5, y - 15, head, 6.6, colors.white, "KR-Bold")
        left += width
    top = y - head_h
    for row in rows:
        rh = 37
        c.setFillColor(colors.white)
        c.setStrokeColor(LINE)
        c.rect(x, top - rh, sum(widths), rh, fill=1, stroke=1)
        left = x
        for value, width in zip(row, widths):
            c.line(left, top, left, top - rh)
            para(c, left + 5, top - 11, value, width - 10, 5.7, 7.4, NAVY)
            left += width
        c.line(x + sum(widths), top, x + sum(widths), top - rh)
        top -= rh
    return top


def page1(c):
    c.setFillColor(colors.HexColor("#EEF7FB"))
    c.rect(0, 0, W, H, fill=1, stroke=0)
    c.setFillColor(NAVY)
    c.rect(0, H - 28, W, 28, fill=1, stroke=0)
    text(c, M, H - 19, "NHN GAME X AI HACKATHON", 7.6, colors.white, "KR-Bold")
    text(c, M, H - 82, "AI UTILIZATION", 9, TEAL, "KR-Bold")
    text(c, M, H - 128, "AI 활용 기술 문서", 28, NAVY, "KR-Bold")
    para(c, M, H - 158, "투기장에서는 칼보단 배팅이다", W - 2 * M, 14, 18, BLUE, "KR-Bold")
    para(c, M, H - 187, "중세 콜로세움에서 팀의 승패를 예측하고 베팅하는 모바일 캐주얼 게임", W - 2 * M, 9.5, 14, GRAY)
    tags = ["기획 지식", "프로그래밍", "UI 아이콘", "BGM"]
    x = M
    for tag in tags:
        c.setFillColor(colors.white)
        c.setStrokeColor(LINE)
        c.roundRect(x, H - 243, 102, 28, 14, fill=1, stroke=1)
        tw = pdfmetrics.stringWidth(tag, "KR-Bold", 8.2)
        text(c, x + (102 - tw) / 2, H - 233, tag, 8.2, NAVY, "KR-Bold")
        x += 116
    draw_image_fit(c, IMG / "01_game_representative.png", M, H - 282, W - 2 * M, 340, colors.white)
    text(c, M + 10, H - 635, "그림 1. 중세 콜로세움과 캐주얼 전투 콘셉트를 보여 주는 게임 대표 화면", 7.2, GRAY)
    rounded_box(c, M, 150, W - 2 * M, 66, "활용 원칙", "AI는 조사·초안·반복 작업을 보조했다. 게임 규칙과 구현 방향은 팀이 결정했고, 코드·UI·음악 결과는 담당자가 직접 검증한 뒤 반영하였다.", colors.white, LINE, 8.2)
    footer(c, "프로젝트 AI 활용 분야: 기획·개발·이미지·음악")


def page2(c):
    page_header(c, 2, "OVERVIEW", "AI 활용 전체 구조", "AI가 처리할 반복 작업과 개발자가 책임질 판단·검증을 분리")
    y = H - 145
    y = para(c, M, y, "프로젝트의 AI 활용은 기획 지식 구조화, 개발 보조, 이미지·음악 제작, 개발자 검증의 네 단계로 구성하였다. 목적은 전체 제작을 자동화하는 것이 아니라 파일 탐색, 흐름 정리, 반복 수정, 테스트 조건 작성에 드는 시간을 줄이는 것이었다.", W - 2 * M, 9.1, 14)
    y -= 18
    flow(c, y, ["기획 지식\n구조화", "개발 보조", "이미지·음악\n제작", "개발자 검증"], h=58, font_size=8)
    y -= 82
    cards = [
        ("Obsidian + LLM", "게임 규칙과 구현 근거를 연결한 자동 기획 아카이브"),
        ("Codex", "관련 파일 조사, 원인 역추적, 구현, 테스트 작성"),
        ("ChatGPT Image", "32×32 UI 아이콘 후보 생성과 반복 수정"),
        ("ChatGPT + SUNO", "장면별 음악 조건 정리와 BGM 후보 생성"),
    ]
    for i, (title, body) in enumerate(cards):
        col, row = i % 2, i // 2
        rounded_box(c, M + col * 258, y - row * 86, 244, 72, title, body, PALE if i % 2 == 0 else colors.white)
    y -= 190
    rounded_box(c, M, y, W - 2 * M, 94, "최종 책임", "AI가 작성한 결과를 그대로 최종 결과로 인정하지 않았다. 위키는 코드와 테스트를 근거로 갱신했고, 구현은 자동 테스트와 Unity Play Mode로 확인했다. 아이콘은 실제 UI 크기에서 가독성을 확인하고, 음악은 장면과 길이에 맞는지 사람이 비교하도록 하였다.", colors.HexColor("#FFF8E8"), colors.HexColor("#E7C979"), 8.3)
    y -= 112
    draw_image_fit(c, IMG / "07_ingame_loading.png", M, y, W - 2 * M, 176, colors.black)
    text(c, M + 10, y - 190, "그림 4. AI 보조 작업을 통합한 뒤 Unity에서 직접 확인한 인게임 로딩 화면", 7.0, GRAY)
    footer(c, "구조: AI 조사·초안 -> 팀원 판단 -> 구현·생성 -> 검증 -> 통합")


def page3(c):
    page_header(c, 3, "PLANNING", "LLM 기반 구현 위키", "문서와 실제 Unity 구현이 함께 갱신되는 게임 기획 아카이브")
    y = H - 144
    left_w, right_w = 285, 198
    y2 = para(c, M, y, "Unity 기능은 Script 하나로 끝나지 않는다. ScriptableObject 데이터, Scene·Prefab 연결, Inspector 설정, 테스트가 함께 동작한다. 이를 반영해 게임 개요, 라운드와 베팅, 자동 전투, 전투 아이템, 콘텐츠 데이터, 저장·보상, UI 흐름, 런타임 구조를 Markdown·Obsidian 위키로 관리하였다.", left_w, 8.8, 13.5)
    y2 -= 12
    text(c, M, y2, "근거 확인 순서", 9.3, BLUE, "KR-Bold")
    y2 -= 16
    for label in ["1  현재 코드·데이터", "2  Scene·Prefab 연결", "3  테스트", "4  최근 Git 기록", "5  기존 문서·해석"]:
        c.setFillColor(PALE)
        c.setStrokeColor(LINE)
        c.roundRect(M, y2 - 31, left_w, 31, 6, fill=1, stroke=1)
        text(c, M + 12, y2 - 20, label, 7.4, GRAY)
        y2 -= 38
    draw_image_fit(c, IMG / "02_obsidian_index.png", M + left_w + 18, y, right_w, 285, colors.HexColor("#252525"))
    text(c, M + left_w + 24, y - 300, "그림 2. Obsidian 구현 위키 목차", 6.8, GRAY)
    y = min(y2, y - 307) - 12
    text(c, M, y, "문서와 구현 근거 연결", 9.6, NAVY, "KR-Bold")
    y -= 18
    flow(c, y, ["라운드와\n베팅.md", "BettingPhase.cs", "BettingPhase\nTests.cs", "변경 이력"], h=50, font_size=7.3)
    y -= 70
    rounded_box(c, M, y, W - 2 * M, 100, "상태 관리 원칙", "실행 경로가 확인된 내용만 implemented로 기록하였다. 선언은 있지만 연결을 확인하지 못한 내용은 partial 또는 review로 분리하였다. 구현과 기존 문서가 충돌하면 현재 구현을 우선하되, 의도와 다르게 보이는 내용은 임의로 확정하지 않았다.", colors.white, LINE, 8.1)
    footer(c, "위키 근거: D:/WIKI/TKB/wiki/index.md, wiki/meta/구현 근거 규칙.md")


def page4(c):
    page_header(c, 4, "PROMPTS", "위키용 주요 프롬프트와 갱신 과정", "문장 생성보다 사실을 판단하는 기준을 프롬프트로 고정")
    y = H - 142
    draw_image_fit(c, IMG / "03_obsidian_content.png", M, y, 214, 172, colors.HexColor("#202020"))
    x = M + 230
    rounded_box(c, x, y, W - M - x, 172, "운영 지시", "현재 Unity 프로젝트의 실제 구현을 기준으로 한국어 Markdown 위키를 관리한다. 코드·데이터, Scene·Prefab, 테스트, Git 기록 순으로 확인한다. 근거가 부족하면 partial 또는 review로 표시한다. 같은 설명은 복사하지 않고 Obsidian 링크로 연결한다.", PALE, LINE, 7.5)
    y -= 194
    rounded_box(c, M, y, W - 2 * M, 94, "기능 조사 프롬프트", "현재 구현을 먼저 조사하고 바로 문서를 작성하지 마라. 관련 Script, ScriptableObject, Scene·Prefab 연결, 테스트를 찾아 실행 경로를 정리해라. 확인된 사실과 해석을 구분하고, 최종 문서에는 근거 경로와 확인일을 기록해라.", colors.white, LINE, 7.9)
    y -= 112
    rounded_box(c, M, y, W - 2 * M, 103, "구현 후 갱신 프롬프트", "최종 git diff와 변경 파일을 기준으로 위키 영향 여부를 확인해라. 게임 규칙이나 데이터 의미가 바뀌었다면 시스템 문서와 변경 이력을 갱신한다. 외부 동작이 바뀌지 않은 리팩터링은 사양 변경으로 기록하지 않는다. 갱신 뒤 내부 링크와 중복 설명을 검사한다.", PALE, LINE, 7.9)
    y -= 121
    rounded_box(c, M, y, W - 2 * M, 96, "실제 갱신 기록", "그림 3은 BGM Catalog 연결과 로딩 전환 안정화 작업을 위키 작업 로그에 기록한 화면이다. 변경 내용뿐 아니라 확인한 Script·Scene·Asset 경로까지 함께 남겨, 이후 문서가 어떤 구현을 근거로 작성됐는지 다시 확인할 수 있게 하였다.", colors.white, LINE, 7.5)
    footer(c, "핵심 지시: 현재 구현 우선 · 추정 금지 · 근거 경로 기록 · 불확실한 내용 review")


def page5(c):
    page_header(c, 5, "PROGRAMMING", "일관된 AI 작업 과정과 병렬 개발", "조사부터 검증까지의 순서를 고정해 작업 과정을 확인 가능하게 구성")
    y = H - 144
    flow(c, y, ["요구사항", "구조 조사", "범위 확정", "계획", "구현"], h=43, font_size=6.8)
    y -= 57
    flow(c, y, ["테스트", "Unity 검증", "피드백", "최종 통합"], x=M + 49, total_w=W - 2 * M - 98, h=43, font_size=7)
    y -= 68
    y = para(c, M, y, "사용자가 원하는 결과와 실제 관찰한 현상을 먼저 설명하고, AI가 Script·Prefab·ScriptableObject·테스트를 조사한 뒤 수정 대상과 확인 대상만 구분하게 했다. 화면 배치와 입력처럼 코드만으로 판단하기 어려운 부분은 팀원이 Unity에서 직접 실행하였다.", W - 2 * M, 8.5, 13)
    y -= 12
    text(c, M, y, "Git worktree를 이용한 병렬 작업", 10, NAVY, "KR-Bold")
    y -= 18
    center_x = W / 2
    rounded_box(c, center_x - 63, y, 126, 43, "Main", "검증된 코드", SKY, BLUE, 6.7)
    branch_y = y - 78
    for i, (label, body) in enumerate([("Unit worktree", "유닛·전투"), ("UI worktree", "화면·입력"), ("Test worktree", "회귀 검증")]):
        bx = M + i * 171
        rounded_box(c, bx, branch_y, 154, 54, label, body, colors.white, LINE, 6.8)
        c.setStrokeColor(TEAL)
        c.line(center_x, y - 43, bx + 77, branch_y)
    y = branch_y - 80
    text(c, M, y, "역할에 맞춘 AI 배치", 10, NAVY, "KR-Bold")
    y -= 18
    roles = [("조사·정리", "빠른 모델"), ("원인·계획", "추론 모델"), ("코드 작성", "추론 모델"), ("검증·보고", "작업별 모델")]
    for i, (title, body) in enumerate(roles):
        rounded_box(c, M + i * 128, y, 116, 67, title, body, PALE if i % 2 == 0 else colors.white, LINE, 7)
    y -= 85
    rounded_box(c, M, y, W - 2 * M, 64, "효과", "작업 폴더를 분리해 Compile·Play Mode·Scene 저장 충돌을 줄였고, 중요한 모델은 반복 검색보다 구조 판단과 코드 작성에 집중하도록 하였다.", colors.HexColor("#FFF8E8"), colors.HexColor("#E7C979"), 7.8)
    footer(c, "공식 참고: https://learn.chatgpt.com/use-cases - 코드베이스 분석, Skill, 테스트·검토 사례")


def page6(c):
    page_header(c, 6, "DEBUGGING", "원인 역추적과 수정 범위 조사", "버그가 보이는 결과에서 시작해 Target·거리·이동·공격 판정을 반대로 추적")
    y = H - 143
    draw_image_fit(c, IMG / "05_ingame_combat.png", M, y, W - 2 * M, 132, colors.HexColor("#D9BD78"))
    y -= 151
    text(c, M, y, "그림 5. 탐색·이동·공격 판정을 점검한 실제 전투 장면", 7.2, GRAY)
    y -= 17
    text(c, M, y, "Unit AI 사례", 10, BLUE, "KR-Bold")
    y -= 17
    y = para(c, M, y, "두 근접 Unit이 서로를 Target으로 잡고 가까이 이동하지만 일정 거리에서 공격하지 않는 문제가 있었다. AI에게 바로 수정을 요청하지 않고 적 탐색부터 공격 판정까지 전체 흐름을 확인하게 했다. 이동과 공격이 서로 다른 위치 기준을 사용했고, 근접 유닛은 Attack Range뿐 아니라 실제 접촉 거리와 이동 허용 오차도 고려해야 함을 확인했다.", W - 2 * M, 8.3, 12.5)
    y -= 13
    flow(c, y, ["적 탐색", "Target 지정", "거리 계산", "이동 위치", "공격 판정"], h=44, font_size=7)
    y -= 68
    rounded_box(c, M, y, 315, 116, "대표 지시", "두 근접 Unit이 일정 거리에서 공격하지 않는다. 바로 수정하지 말고 Target 탐색부터 이동·공격 거리 계산까지 확인해라. Search Range, Attack Range, 실제 Unit 크기와 기준 Transform이 다르게 계산되는지도 확인하고, 근접·원거리 상황을 회귀 테스트로 남겨라.", PALE, LINE, 7.2)
    x = M + 330
    c.setFillColor(colors.white)
    c.setStrokeColor(LINE)
    c.roundRect(x, y - 116, 171, 116, 7, fill=1, stroke=1)
    text(c, x + 12, y - 18, "기준점 차이", 8.5, NAVY, "KR-Bold")
    c.setFillColor(BLUE)
    c.circle(x + 55, y - 66, 8, fill=1, stroke=0)
    c.setFillColor(ORANGE)
    c.circle(x + 118, y - 84, 8, fill=1, stroke=0)
    arrow(c, x + 64, y - 69, x + 108, ORANGE)
    text(c, x + 23, y - 101, "Transform", 6.3, BLUE, "KR-Bold")
    text(c, x + 91, y - 101, "Ground Root", 6.3, ORANGE, "KR-Bold")
    y -= 136
    rounded_box(c, M, y, W - 2 * M, 76, "HP Bar 수정 범위", "Inspector 값만 바꾸는 문제가 아니었다. Unit의 HitPosition을 화면 좌표로 바꾼 뒤 Runtime에서 위치와 크기를 다시 적용하고 있어 Script 계산과 Prefab 설정을 함께 확인했다. AI가 줄여 준 시간은 코드 입력보다 값이 다시 바뀌는 위치를 찾는 시간이었다.", colors.white, LINE, 7.7)
    footer(c, "코드 근거: UnitDecisionAgent, DecisionSystem, EngagementSlotSystem, UnitRuntimeArchitectureTests")


def page7(c):
    page_header(c, 7, "TOOLS & SAFETY", "개발 도구 제작과 안정성 보완", "반복 테스트 환경을 자동화하고 정상 경로 밖의 입력도 회귀 테스트로 고정")
    y = H - 142
    pw = 158
    draw_image_fit(c, IMG / "04_save_debug_tool.png", M, y, 170, 158, colors.HexColor("#2B2B2B"))
    rounded_box(c, M + 186, y, 151, 158, "UnitSystemMigration", "구버전 Unit Prefab을 새 구조로 일괄 변환하고 누락된 Component와 기준점을 정리하였다.", PALE, LINE, 7.2)
    rounded_box(c, M + 351, y, 151, 158, "RoundDataEditor", "Team A/B의 2×3 배치를 실제 전장과 비슷한 Grid 형태로 편집하도록 Inspector를 구성하였다.", colors.white, LINE, 7.2)
    y -= 177
    items = [
        ("테스트 상태 생성", "원하는 재화·스테이지 상태를 즉시 만들어 반복 플레이 시간을 줄였다."),
        ("구버전 일괄 변환", "Unit Prefab에 같은 구조와 Component 규칙을 적용해 누락을 줄였다."),
        ("데이터 편집 개선", "배열 대신 실제 배치에 가까운 Grid로 RoundData를 수정하였다."),
    ]
    for i, (title, body) in enumerate(items):
        rounded_box(c, M + i * 172, y, pw, 77, title, body, PALE if i != 1 else colors.white, LINE, 6.9)
    y -= 99
    text(c, M, y, "Battle Item Drag 예외 처리", 10, BLUE, "KR-Bold")
    y -= 18
    y = para(c, M, y, "Meteor와 Mercenary는 전장 위치를 지정해야 하므로 정상 입력만 확인해서는 부족했다. 처음 누른 Pointer ID를 저장해 다른 입력을 무시하고, 전장 밖에서는 DraggingInvalid 상태로 전환하였다. Targeting 중 UI가 사라지면 상태와 전투 속도를 복구하고, Mercenary 세 명이 모두 전장 안에 생성되는지도 계산하였다.", W - 2 * M, 8.1, 12.2)
    y -= 10
    draw_image_fit(c, IMG / "06_ingame_drag_range.png", M, y, 278, 137, colors.HexColor("#D9BD78"))
    rounded_box(c, M + 293, y, 208, 137, "검증 항목", "전장 안·밖 Drag\n다른 Pointer의 입력 무시\nTargeting 취소 시 상태 복구\nMercenary 생성 범위 계산\n실패 시 Gold·사용 상태 복구", PALE, LINE, 7.1)
    y -= 155
    rounded_box(c, M, y, W - 2 * M, 56, "검증한 예외", "다른 Pointer의 PointerUp · 전장 밖 Drag · Targeting 중 비활성화 · 아이템 실패 시 Gold와 사용 상태 복구", colors.HexColor("#FFF8E8"), colors.HexColor("#E7C979"), 7.5)
    footer(c, "코드 근거: SaveDebugWindow, UnitSystemMigration, RoundDataEditor, UI_CombatItemTargetingController")


def page8(c):
    page_header(c, 8, "IMAGE GENERATION", "ChatGPT를 활용한 32×32 UI 아이콘 제작", "기존 UI 시트를 기준으로 스타일을 고정하고 수정 요청을 단계적으로 반복")
    y = H - 140
    draw_image_fit(c, IMG / "08_ingame_betting.png", M, y, 188, 292, colors.HexColor("#D9C47E"))
    text(c, M + 8, y - 307, "그림 8. 아이콘이 적용된 베팅 화면", 6.8, GRAY)
    x = M + 204
    rounded_box(c, x, y, W - M - x, 122, "대표 프롬프트", "32×32 픽셀 아이콘. 중세 콜로세움에서 팀에 베팅해 돈을 버는 모바일 캐주얼 게임이며, 3 Match Puzzle처럼 밝고 읽기 쉬운 스타일을 사용한다. 작은 크기에서 구분되는 실루엣, 투명 배경 PNG, 텍스트·워터마크 제외 조건을 유지한다.", PALE, LINE, 7.3)
    draw_image_fit(c, IMG / "09_ingame_top_ui.png", x, y - 138, W - M - x, 83, colors.HexColor("#182334"))
    text(c, x + 8, y - 237, "그림 9. 재화·옵션 아이콘의 실제 상단 UI 적용", 6.5, GRAY)
    rounded_box(c, x, y - 256, W - M - x, 99, "수정과 선별", "초안에서 문양과 색상이 UI 목적에 맞지 않으면 입장권의 월계관, 별 모양, 지폐 색상처럼 수정 조건을 구체적으로 다시 전달하였다. 결과는 사람이 작은 화면에서 비교해 선택하였다.", colors.white, LINE, 7.1)
    y -= 335
    rounded_box(c, M, y, W - 2 * M, 94, "실제 적용 과정", "ChatGPT가 생성한 후보를 그대로 사용하지 않고 투명 배경과 가장자리를 확인한 뒤 32×32 크기로 정리하였다. Unity에서는 픽셀 경계가 흐려지지 않도록 Point Filter를 적용하고, 베팅 화면과 상단 HUD에서 실루엣과 색상 구분이 유지되는지 확인하였다.", colors.white, LINE, 7.5)
    y -= 111
    flow(c, y, ["공통 스타일", "후보 생성", "수정 요청", "32×32 정리", "Unity 적용"], h=42, font_size=6.7)
    y -= 59
    rounded_box(c, M, y, W - 2 * M, 68, "사람이 담당한 최종 판단", "아이콘의 의미, 수정 방향, 실제 UI 배치와 최종 채택 여부는 개발자가 결정하였다. AI는 동일한 시각 조건에서 여러 후보를 빠르게 비교하기 위한 제작 보조 도구로 사용하였다.", colors.HexColor("#FFF8E8"), colors.HexColor("#E7C979"), 7.4)
    footer(c, "적용 확인: 베팅 화면의 팀·재화 아이콘과 상단 HUD의 재화·옵션 아이콘")


def page9(c):
    page_header(c, 9, "MUSIC PROMPTING", "ChatGPT와 SUNO를 활용한 BGM 제작", "게임 장면과 길이 조건을 음악 생성 프롬프트로 구체화")
    y = H - 142
    draw_image_fit(c, IMG / "10_chatgpt_bgm_prompt.png", M, y, 214, 190, colors.black)
    x = M + 230
    rounded_box(c, x, y, W - M - x, 190, "대표 전투 BGM 프롬프트", "Exactly 30-second instrumental battle loop for a colorful medieval casual mobile game. Start immediately with no intro. Energetic lute, fast fiddle, frame drums, hand claps and bright brass. Cute gladiators fight while spectators cheer and bet gold. Instrumental only, no vocals, no fade-out, no dark fantasy.", PALE, LINE, 6.8)
    y -= 208
    y = para(c, M, y, "단순히 ‘중세 음악’을 요청하면 어둡고 웅장한 곡이 나오기 쉬워 밝은 모바일 캐주얼 게임, 축제형 콜로세움, 귀여운 검투사, 금화를 거는 관중이라는 장면 정보를 함께 제공하였다. 타이틀은 장난스러운 주제 선율, 베팅은 동전 소리와 계산하는 긴장감, 전투는 즉시 시작되는 빠른 리듬을 중심으로 구분하였다.", W - 2 * M, 8.0, 12.2)
    y -= 11
    draw_image_fit(c, IMG / "11_suno_page.png", M, y, W - 2 * M, 172, colors.HexColor("#121214"))
    y -= 190
    flow(c, y, ["상황 정의", "프롬프트", "SUNO 생성", "후보 비교", "30초 편집", "Unity 적용"], h=46, font_size=6.4)
    y -= 66
    rounded_box(c, M, y, W - 2 * M, 85, "사람이 담당한 최종 판단", "음악 생성 서비스가 요청 길이를 정확히 지키지 않을 수 있으므로, 실제 장면에 사용할 구간 선택과 30초 편집은 개발자가 수행하도록 계획하였다. 현재 저장소에서 음원 파일은 확인되지 않아 이 문서에서는 프롬프트 설계와 후보 생성 과정까지만 확정적으로 기술한다.", colors.HexColor("#FFF8E8"), colors.HexColor("#E7C979"), 7.7)
    footer(c, "생성 흐름: ChatGPT 프롬프트 설계 -> SUNO 후보 생성 -> 개발자 비교·편집")


def page10(c):
    page_header(c, 10, "SOURCES & CONCLUSION", "외부 에셋·오픈소스 출처와 결론", "확인 가능한 출처는 명시하고, 확인되지 않은 정보는 추정하지 않음")
    y = H - 138
    rows = [
        ["오픈소스", "DOTween", "UI·연출 Tween", "dotween.demigiant.com\nDOTween 라이선스"],
        ["상용 확장", "DOTween Pro", "Animation·Path 확장", "dotween.demigiant.com/pro.php\n유효 라이선스 필요"],
        ["오픈소스", "MCP for Unity", "AI-Unity 연동", "github.com/CoplayDev/unity-mcp\nMIT"],
        ["오픈소스", "SerializeReference Extensions", "Inspector 지원", "github.com/mackysoft/\nUnity-SerializeReferenceExtensions"],
        ["외부 에셋", "Pixel Sprite Effects pack", "전투 VFX", "프로젝트 내 사용\n원 구매 기록 기준"],
        ["외부 에셋", "RPG Icons Pixel Art", "스킬·UI 아이콘", "프로젝트 내 사용\n원 구매 기록 기준"],
        ["AI 생성", "ChatGPT 아이콘", "재화·메뉴·능력치", "생성 기록\n선별·후편집"],
        ["AI 생성", "SUNO BGM 후보", "타이틀·베팅·전투", "생성 기록\n후보 비교·편집 필요"],
    ]
    y = source_table(c, M, y, [52, 112, 104, 235], rows) - 18
    text(c, M, y, "결론", 11, NAVY, "KR-Bold")
    y -= 18
    para(c, M, y, "AI를 사용하며 가장 크게 줄어든 것은 코드 입력 시간이 아니었다. 문제가 어느 파일에서 시작되는지 찾는 시간, 여러 Prefab을 같은 규칙으로 수정하는 시간, 테스트 상태를 반복해서 만드는 시간, 예외 상황과 회귀 조건을 정리하는 시간이 줄었다.\n\nAI가 만든 결과는 코드·테스트·Unity 실행으로 확인했고, 검증된 결과만 통합하였다. AI는 개발자를 대신하기보다 판단 전에 필요한 조사와 반복 작업을 맡아 주는 개발 보조 도구로 가장 유용했다.", W - 2 * M, 8.2, 12.5)
    y -= 113
    rounded_box(c, M, y, W - 2 * M, 62, "최종 흐름", "문제·아이디어 정의 -> AI 조사·초안 -> 팀원 판단 -> 구현·생성 -> 테스트·실행 확인 -> 피드백 수정 -> 검증된 결과만 통합", SKY, BLUE, 7.5)
    y -= 81
    text(c, M, y, "제공 이미지 수록 확인", 8.8, NAVY, "KR-Bold")
    image_rows = [
        ("1  게임 대표 이미지", "1쪽"), ("2  Obsidian 목차", "3쪽"),
        ("3  Obsidian 내용", "4쪽"), ("4  SaveDebugTool", "7쪽"),
        ("5  인게임 전투", "6쪽"), ("6  드래그 범위", "7쪽"),
        ("7  인게임 로딩", "2쪽"), ("8  인게임 베팅", "8쪽"),
        ("9  인게임 상단 UI", "8쪽"), ("10 ChatGPT BGM", "9쪽"),
        ("11 SUNO 페이지", "9쪽"),
    ]
    for i, (label, page) in enumerate(image_rows):
        col, row = i % 2, i // 2
        bx = M + col * 250
        by = y - 16 - row * 13
        text(c, bx, by, label, 6.2, GRAY)
        text(c, bx + 190, by, page, 6.2, BLUE, "KR-Bold")
    footer(c, "근거: Assets/Scripts · Assets/Editor · D:/WIKI/TKB/wiki · Packages/manifest.json")


def build():
    register_fonts()
    OUT.parent.mkdir(parents=True, exist_ok=True)
    c = canvas.Canvas(str(OUT), pagesize=A4)
    c.setTitle("AI 활용 기술 문서 | 투기장에서는 칼보단 배팅이다")
    c.setAuthor("중간에서 보면 대전")
    pages = [page1, page2, page3, page4, page5, page6, page7, page8, page9, page10]
    for index, page in enumerate(pages):
        page(c)
        if index < len(pages) - 1:
            c.showPage()
    c.save()
    print(OUT)


if __name__ == "__main__":
    build()
