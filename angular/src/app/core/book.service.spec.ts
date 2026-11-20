import { BookService } from './book.service';

describe('BookService', () => {
  let service: BookService;

  beforeEach(() => {
    localStorage.removeItem('apl-uppgift.books');
    service = new BookService();
  });

  it('updates a book without changing its id', () => {
    expect(
      service.update(1, {
        title: 'The Hobbit: Revised',
        author: 'J.R.R. Tolkien',
        publicationDate: '1937-09-21',
      }),
    ).toBe(true);

    expect(service.get(1)).toEqual({
      id: 1,
      title: 'The Hobbit: Revised',
      author: 'J.R.R. Tolkien',
      publicationDate: '1937-09-21',
    });
  });

  it('deletes an existing book and leaves other books intact', () => {
    expect(service.delete(2)).toBe(true);

    expect(service.get(2)).toBeUndefined();
    expect(service.list().map((book) => book.id)).toEqual([1, 3]);
  });

  it('does not update or delete a book that does not exist', () => {
    expect(
      service.update(99, { title: 'Missing', author: 'Nobody', publicationDate: '2020-01-01' }),
    ).toBe(false);
    expect(service.delete(99)).toBe(false);
  });

  it('keeps added, updated, and deleted books after the service is recreated', () => {
    service.add({ title: 'Dune', author: 'Frank Herbert', publicationDate: '1965-08-01' });
    service.update(1, {
      title: 'The Hobbit: Revised',
      author: 'J.R.R. Tolkien',
      publicationDate: '1937-09-21',
    });
    service.delete(2);

    const reloadedService = new BookService();

    expect(reloadedService.get(4)).toEqual({
      id: 4,
      title: 'Dune',
      author: 'Frank Herbert',
      publicationDate: '1965-08-01',
    });
    expect(reloadedService.get(1)?.title).toBe('The Hobbit: Revised');
    expect(reloadedService.get(2)).toBeUndefined();
  });
});
