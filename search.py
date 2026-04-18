import urllib.request
from html.parser import HTMLParser

class MyHTMLParser(HTMLParser):
    def __init__(self):
        super().__init__()
        self.in_snippet = False
        self.snippets = []
        self.current_snippet = []

    def handle_starttag(self, tag, attrs):
        if tag == 'a':
            for name, value in attrs:
                if name == 'class' and 'result__snippet' in value:
                    self.in_snippet = True

    def handle_endtag(self, tag):
        if tag == 'a' and self.in_snippet:
            self.in_snippet = False
            self.snippets.append(''.join(self.current_snippet))
            self.current_snippet = []

    def handle_data(self, data):
        if self.in_snippet:
            self.current_snippet.append(data)

url = 'https://html.duckduckgo.com/html/?q=site:github.com+%22InkPresenter%22+%22UpdateDefaultDrawingAttributes%22+%22Wpf%22'
req = urllib.request.Request(url, headers={'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)'})
try:
    with urllib.request.urlopen(req) as response:
        html = response.read().decode('utf-8')
        parser = MyHTMLParser()
        parser.feed(html)
        for i, s in enumerate(parser.snippets):
            print(f'Snippet {i}: {s}')
except Exception as e:
    print(e)
