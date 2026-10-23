import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { SpecializationService } from '../../../core/services/specialization.service';
import { SPECIALIZATIONS } from '../../../../testing/test-data';
import { SpecializationsComponent } from './specializations.component';

describe('SpecializationsComponent (UC-11 Manage specialties)', () => {
  let fixture: ComponentFixture<SpecializationsComponent>;
  let component: SpecializationsComponent;
  let service: jasmine.SpyObj<SpecializationService>;

  beforeEach(() => {
    service = jasmine.createSpyObj<SpecializationService>('SpecializationService', ['getAll', 'create', 'update', 'delete']);
    service.getAll.and.returnValue(of(SPECIALIZATIONS));
    TestBed.configureTestingModule({
      imports: [SpecializationsComponent],
      providers: [{ provide: SpecializationService, useValue: service }]
    });
    fixture = TestBed.createComponent(SpecializationsComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('validates the name before calling the API', () => {
    component.name = ' x ';
    component.save();
    expect(service.create).not.toHaveBeenCalled();
    expect(component.error()).toContain('at least 2 characters');
  });

  it('creates a specialty with trimmed values', () => {
    service.create.and.returnValue(of(SPECIALIZATIONS[0]));
    component.name = '  Neurology ';
    component.description = '   ';

    component.save();

    expect(service.create).toHaveBeenCalledWith({ name: 'Neurology', description: null });
    expect(component.name).toBe('');
  });

  it('edits an existing specialty', () => {
    service.update.and.returnValue(of(SPECIALIZATIONS[1]));
    component.edit(SPECIALIZATIONS[1]);
    component.name = 'Skin care';

    component.save();

    expect(service.update).toHaveBeenCalledWith(2, { name: 'Skin care', description: 'Skin' });
    expect(component.editingId()).toBeNull();
  });

  it('shows why a specialty with doctors cannot be deleted', () => {
    spyOn(window, 'confirm').and.returnValue(true);
    service.delete.and.returnValue(throwError(() =>
      new HttpErrorResponse({ status: 400, error: { message: "Reassign this specialization's doctors before deleting it." } })));

    component.remove(SPECIALIZATIONS[0]);

    expect(component.error()).toContain('Reassign');
  });
});
