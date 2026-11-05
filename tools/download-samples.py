"""Downloads every chart published on majdata.net, for the sample equivalence test.

Usage: python tools/download-samples.py <output folder>

Each chart is saved as <output folder>/<chart id>/maidata.txt. Files that already exist are kept.
"""
import concurrent.futures
import json
import os
import sys
import urllib.request

LIST_URL = "https://majdata.net/api3/api/maichart/list"
CHART_URL = "https://cdn.majdata.net/MaiCharts/{}/maidata.txt"
HEADERS = {"User-Agent": "Mozilla/5.0 MajSimaiSampleFetcher"}


def fetch(url):
    request = urllib.request.Request(url, headers=HEADERS)
    with urllib.request.urlopen(request, timeout=120) as response:
        return response.read()


def download(output, chart_id):
    path = os.path.join(output, chart_id, "maidata.txt")
    if os.path.exists(path):
        return True
    error = None
    for _ in range(4):
        try:
            content = fetch(CHART_URL.format(chart_id))
            os.makedirs(os.path.dirname(path), exist_ok=True)
            with open(path, "wb") as file:
                file.write(content)
            return True
        except Exception as e:
            error = e
    print(f"Failed to download {chart_id}: {error}", flush=True)
    return False


def main():
    if len(sys.argv) != 2:
        print(__doc__)
        return 2
    output = sys.argv[1]
    chart_ids = [chart["id"] for chart in json.loads(fetch(LIST_URL))]
    print(f"Charts: {len(chart_ids)}", flush=True)
    with concurrent.futures.ThreadPoolExecutor(32) as executor:
        results = list(executor.map(lambda chart_id: download(output, chart_id), chart_ids))
    failed = results.count(False)
    print(f"Downloaded: {len(results) - failed}, failed: {failed}")
    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
